using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

// TODO cancellationtoken, make this a task perhaps
// TODO make run button a reactivecommand. it can b enabled when you have valid directories AND when this ISNT running
// TODO plug in settings for sampleone etc
// TODO cleanup
namespace Studio_One_File_Finder
{
	enum FileType
	{
		MediaPool,
		SampleOne,
		Impact
	}
	class FileUpdater
	{
		const int XML_WRAP_CHARACTER_COUNT = 100;
		const string MEDIA_POOL = "Song/mediapool.xml";
		const string BACKUP_FILE_EXTENSION = ".s1filefinderbackup";
		const string FX_EXTENSTION = ".fxpreset";
		private static string FILE_TYPE_NODES(FileType fType)
		{
			switch(fType)
			{
				case FileType.MediaPool: return "//AudioClip/Url";
				case FileType.SampleOne: return "//Zone/Attributes";
				case FileType.Impact: return "//List/Url";
				default: return null;
			}
		}
		Dictionary<FileType, string> _nodesToFind;

		public bool CurrentlyRunning = false;

		private bool _updateSampleOneRefs;

		public delegate void Callback(string message);
		public delegate void CallbackAlert(string message, string title="Alert");
		public delegate Task<bool> CallbackPrompt(string title, string message, string yes, string no);
		CallbackAlert _currentHandler; // TODO move these into the constructor and stop passing them in through delete, restore, etc.
		Callback _currentOutput;
		FilePreferencesViewModel.ClearConsole _clearConsole;// TODO move ClearConsole out of fpvm into here

		private string _currentSongFolderName;

		Dictionary<FileType, uint> _fileTypeCounts;
		uint _refUpdateCount;
		uint _projectsUpdated;
		int count; // TODO rename
		private Dictionary<string, string?> _discoveredFiles;
		private List<string> _sampleFolders;
		private ExtraSettings _userConfig;
		private List<string> _filesRestored;
		private List<string> _backupsDeleted;
		public FileUpdater(FilePreferencesViewModel.ClearConsole clearConsole)
		{
			_clearConsole = clearConsole;
			InitClass(true);
		}
		private void InitClass(bool resetCachedPaths=false)
		{
			_refUpdateCount = 0;
			_fileTypeCounts = new();
			_projectsUpdated = 0;
			_sampleFolders = new();
			_nodesToFind = new()
			{
				{ FileType.MediaPool, FILE_TYPE_NODES(FileType.MediaPool) }
			};
			if (resetCachedPaths) _discoveredFiles = new();
			InitBackupVars();
		}
		private void InitBackupVars()
		{
			_filesRestored = new();
			_backupsDeleted = new();
		}
		private void ValidatePaths(List<string> dirs)
		{
			for (int i = dirs.Count - 1; i >= 0; i--)
			{
				if (dirs[i] == null || dirs[i].Length == 0)
				{
					dirs.RemoveAt(i);
				}
				dirs[i] = Path.GetFullPath(dirs[i]);
				if (!Directory.Exists(dirs[i]))
				{
					_currentHandler($"Invalid directory: {dirs[i]}");
					dirs.RemoveAt(i);
				}
			}
		}
		private void DoStuffWithSongsInThisDir(List<string> projectDirectories, Callback modifier)
		{
			count = 0;
			HashSet<string> songFolders = new();
			void FindSongFolders(DirectoryInfo currentDir)
			{
				var songFiles = Directory.GetFiles(currentDir.FullName, $"*.song").ToList();
				if (songFiles.Count > 0 && currentDir.Name != "History") // if we excluding autosaves
				{
					songFolders.Add(currentDir.FullName);
					//return; // if we only counting song folders
				}
				foreach (var item in currentDir.EnumerateDirectories())
				{
					FindSongFolders(item);
				}
			}

			foreach (string projectsDir in projectDirectories)
			{
				int countBefore = songFolders.Count;
				FindSongFolders(new DirectoryInfo(projectsDir));

				if (songFolders.Count - countBefore == 0)
				{
					_currentOutput($"No song folders found in {projectsDir}");
					continue;
				}
			}
			foreach (string songFolderPath in songFolders)
			{
				modifier(songFolderPath);
				count++;
				if (count > 20)
				{
					break;
				}
			}
		}
		public void UpdateFiles(List<string> sampleDirectories, List<string> projectDirectories, List<FileType> typesToUpdate, ExtraSettings config, CallbackAlert handler, Callback output)
		{
			if (CurrentlyRunning)
			{
				handler("The file updater is already running!", "Holup");
				return;
			}
			CurrentlyRunning = true;
			_clearConsole();

			InitClass();
			foreach (var fType in typesToUpdate)
			{
				_nodesToFind[fType] = FILE_TYPE_NODES(fType);
				_fileTypeCounts[fType] = 0;
			}
			_userConfig = config;

			_currentHandler = handler;
			_currentOutput = output;
			ValidatePaths(sampleDirectories);
			ValidatePaths(projectDirectories);

			_sampleFolders = sampleDirectories;

			if (sampleDirectories.Count == 0)
			{
				handler("There are no valid sample directories.");
				return;
			}
			if (projectDirectories.Count == 0)
			{
				handler("There are no valid project directories.");
				return;
			}

			DoStuffWithSongsInThisDir(projectDirectories, UpdateSong);
			string finalString = $"Updated {_refUpdateCount} sample references ({_projectsUpdated} songs)";
			foreach (var fTypeCount in _fileTypeCounts)
			{
				finalString += $"\n{fTypeCount.Value} of those were {fTypeCount.Key} updates";
			}
			handler(finalString);
			CurrentlyRunning = false;
		}
		private void UpdateSong(string songFolderPath)
		{
			var songFiles = Directory.GetFiles(songFolderPath, "*.song").Where(x => !Path.GetFileName(x).StartsWith("._")).ToList();
			//throw new Exception("need to exclude ._*.song files...");
			if (songFiles.Count == 0)
			{
				// TODO maybe check autosaves here (could make a setting for this)

				_currentOutput($"No song files found in {songFolderPath} (it may have some autosaves)...skipping to next");
				return;
			}
			foreach (var songFile in songFiles)
			{
				LoadProject(songFile);
			}
		}
		/// <summary>
		/// Write out the entry
		/// </summary>
		/// <param name="destination"></param>
		/// <param name="entry"></param>
		/// <param name="fType"></param>
		private void WriteEntryIfNeeded(ZipArchive destination, ZipArchiveEntry entry, FileType fType)
		{
			var destinationEntry = destination.CreateEntry(entry.FullName);
			string alterFileResult;
			uint countBeforeCurEntry = _refUpdateCount;
			using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
			{
				alterFileResult = AlterFile(reader, fType);
			}
			// keep our hands off the entry if we can
			if (_refUpdateCount - countBeforeCurEntry > 0)
			{
				if (_fileTypeCounts.ContainsKey(fType))
				{
					_fileTypeCounts[fType] += _refUpdateCount - countBeforeCurEntry;
				}
				using (var writer = new StreamWriter(destinationEntry.Open(), Encoding.UTF8))
				{
					writer.Write(alterFileResult);
				}
			}
			else
			{
				CopyEntry(destination, entry);
			}
		}
		private void CopyEntry(ZipArchive destination, ZipArchiveEntry entry)
		{
			var newEntry = destination.CreateEntry(entry.FullName); // TODO change compressionlevel this maybe
			using (var entryStream = entry.Open())
			using (var newEntryStream = newEntry.Open())
			{
				entryStream.CopyTo(newEntryStream);
			}
		}
		public void LoadProject(string sourceFilePath)
		{
			_currentOutput("*****************************************");
			_currentOutput($"Finding samples for {sourceFilePath}");
			_currentOutput("*****************************************");
			var splitPath = sourceFilePath.Split(Path.DirectorySeparatorChar);
			if (splitPath.Length == 1)
			{
				_currentSongFolderName = "";
			}
			else
			{
				_currentSongFolderName = splitPath[splitPath.Length - 2];
			}
			uint countBeforeCurFile = _refUpdateCount;
			// TODO test inputting both / and \\
			string tempFilePath = sourceFilePath + "temp"; // will this work lmao
			if (File.Exists(tempFilePath))
			{
				File.Delete(tempFilePath);
			}
			using (FileStream sourceStream = new FileStream(sourceFilePath, FileMode.Open))
			using (FileStream destinationStream = new FileStream(tempFilePath, FileMode.Create))
			using (ZipArchive source = new ZipArchive(sourceStream, ZipArchiveMode.Read))
			using (ZipArchive destination = new ZipArchive(destinationStream, ZipArchiveMode.Create))
			{
				foreach (ZipArchiveEntry entry in source.Entries)
				{
					// TODO could probs refactor
					if (entry.FullName == MEDIA_POOL && _nodesToFind.ContainsKey(FileType.MediaPool))
					{
						WriteEntryIfNeeded(destination, entry, FileType.MediaPool);
					}
					else if (entry.FullName.StartsWith("Presets/Synths/") && entry.Name.Contains(FX_EXTENSTION))
					{
						if (_nodesToFind.ContainsKey(FileType.SampleOne) && entry.Name.Contains("SampleOne"))
						{
							_currentOutput("Checking SampleOne files...");
							WriteEntryIfNeeded(destination, entry, FileType.SampleOne);
						}
						else if (_nodesToFind.ContainsKey(FileType.Impact) && entry.Name.Contains("Impact"))
						{
							_currentOutput("Checking Impact files...");
							WriteEntryIfNeeded(destination, entry, FileType.Impact);
						}
						else
						{
							CopyEntry(destination, entry);
						}
					}
					else
					{
						CopyEntry(destination, entry);
					}
				}
			}
			if (_refUpdateCount - countBeforeCurFile == 0)
			{
				// Don't modify files
				File.Delete(tempFilePath);
			}
			else
			{
				string newBackupPath = sourceFilePath + BACKUP_FILE_EXTENSION;
				if (!File.Exists(newBackupPath)) // if a backup exists, leave it be
				{
					File.Move(sourceFilePath, newBackupPath);
				}

				File.Move(tempFilePath, sourceFilePath, true);
				_projectsUpdated++;
			}
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="currentDir"></param>
		/// <param name="fileName"></param>
		/// <returns>null if the file isn't found</returns>
		string? SearchMyDirOfficer(DirectoryInfo currentDir, string fileName)
		{
			FileInfo? triedFile;
			try
			{
				triedFile = currentDir.EnumerateFiles().ToList().FirstOrDefault(x => x.Name == fileName);
			}
			catch (Exception ex)
			{
				// We probably just don't have permissions.
				//add to an error log
				_currentOutput($"Problem with searching folder for samples:\n{ex.Message}");
				return null;
			}

			if (triedFile != null)
			{
				return triedFile.FullName;
			}
			var dirs = currentDir.EnumerateDirectories();
			foreach (var dir in dirs)
			{
				var res = SearchMyDirOfficer(dir, fileName);
				if (res != null) return res;
			}
			return null;
		}
		string AlterFile(StreamReader fileData, FileType fileType)
		{
			XmlReaderSettings settings = new XmlReaderSettings { NameTable = new NameTable() };
			XmlNamespaceManager xmlns = new XmlNamespaceManager(settings.NameTable);
			xmlns.AddNamespace("x", "peepeepoopoo");
			XmlParserContext context = new XmlParserContext(null, xmlns, "", XmlSpace.Default);
			XmlReader reader = XmlReader.Create(fileData, settings, context);
			XmlDocument xmlDoc = new();
			xmlDoc.Load(reader);
			XmlNodeList? elements = xmlDoc.SelectNodes(_nodesToFind[fileType]);
			if (elements != null)
			{
				UpdateXmlNodes(elements);
			}

			return Beautify(xmlDoc);
		}
		private string GetFileName(string fullPath, char dirSeparator='/')
		{
			string[] dirName = fullPath.Split(dirSeparator);
			return dirName[dirName.Length - 1];
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="oldSamplePath">A string for the old sample path. Assumed to not be null or empty.</param>
		/// <returns></returns>
		private string? SearchForFile(string oldSamplePath)
		{
			string fileName = GetFileName(oldSamplePath);
			string? matchingFile;
			if (_discoveredFiles.TryGetValue(fileName, out matchingFile))
			{
				_currentOutput($"{fileName} was cached, nice.");
			}
			else
			{
				foreach (var path in _sampleFolders)
				{
					matchingFile = SearchMyDirOfficer(new DirectoryInfo(path), fileName);
					if (matchingFile != null) break;
				}
				_discoveredFiles[fileName] = matchingFile; // cache to make our other file searches a billion times faster
			}
			return matchingFile;
		}
		/// <param name="path">MUST be format S1 stores in (uses forward slashes)</param>
		/// <returns>Whether we think it's inside the media or bounces folder.</returns>
		private bool FileAppearsToBeARelativePath(string path)
		{
			var pathFolders = path.Split('/');
			if (pathFolders.Length < 3) return false;
			if (pathFolders[pathFolders.Length - 3] == _currentSongFolderName) // songname
			{
				if (pathFolders[pathFolders.Length - 2] == "Media" || pathFolders[pathFolders.Length - 2] == "Bounces")
				{
					// check if its in the path
					return true;
				}
			}
			return false;
		}
		/// <summary>
		/// Search the given nodes for a url attribute and update it if possible.
		/// </summary>
		/// <param name="elements"></param>
		void UpdateXmlNodes(XmlNodeList elements)
		{
			string? currentFilePath = null;
			void UpdateXmlNode(XmlNode element, bool modifiedFileName=false)
			{
				if (!modifiedFileName)
				{
					currentFilePath = element.Attributes?.GetNamedItem("url")?.Value;
					if (string.IsNullOrEmpty(currentFilePath)) return;
					if (currentFilePath.StartsWith("package://"))
					{
						return;
					}
				}
				string fileName = GetFileName(currentFilePath);
				if (!modifiedFileName)
				{
					if (FileAppearsToBeARelativePath(currentFilePath))
					{
						return;
					}
					string[] fileDir = currentFilePath.Split("file:///");
					if (File.Exists(fileDir[fileDir.Length - 1]))
					{
						if (!_userConfig.OverwriteValidPaths)
						{
							_currentOutput($"{fileName} already exists with the current path. Not overwriting.");
							return;
						}
						else
						{
							_currentOutput($"{fileName} already exists with the current settings, but we are going to overwrite it if we can.");
						}
					}
				}
				var matchingFile = SearchForFile(currentFilePath);
				if (matchingFile != null)
				{
					string newAttrib = "file:///" + matchingFile.Replace("\\", "/");
					if (currentFilePath == newAttrib)
					{
						// No need to overwrite, the link is already good
						_currentOutput($"{fileName} was already linked correctly");
						return;
					}
					_currentOutput($"FOUND A MATCH!!! {fileName} found in {matchingFile}");
					// rewrite
					element.Attributes!.GetNamedItem("url")!.Value = newAttrib;
					_refUpdateCount++;
				}
				else
				{
					if (_userConfig.UpdateDuplicateFiles && FilenameAppearsToBeDuplicated(fileName) && !modifiedFileName)
					{
						currentFilePath = RemoveDuplicateFileRegex(currentFilePath);
						_currentOutput($"{fileName} appears to be a duplicate file. Checking for {GetFileName(currentFilePath)} instead...");
						UpdateXmlNode(element, true);
					}
					else
					{
						_currentOutput($"Couldn't find a match for {fileName} ...");
					}
				}
			}
			foreach (XmlNode element in elements) // TODO a lot of this could maybe be refactored? we might have to call this method again on files we're searching that we modify
			{
				UpdateXmlNode(element);
			}

		}
		string DUPE_PATTERN = @"\([0-9]+\)\.[^.]*$";
		string DUPE_PATTERN_REPLACE = @"\([0-9]+\)(?=\.[^.]*$)";
		private bool FilenameAppearsToBeDuplicated(string fName)
		{
			return Regex.IsMatch(fName, DUPE_PATTERN);
		}
		private string RemoveDuplicateFileRegex(string filePath)
		{
			return Regex.Replace(filePath, DUPE_PATTERN_REPLACE, "");
		}
		/// <summary>
		/// I hate this. I want to copy s1's format EXACTLY (or as close as possible). This probably doesn't matter a single bit
		/// </summary>
		/// <param name="doc"></param>
		/// <returns></returns>
		public string Beautify(XmlDocument doc)
		{
			string GetInnerXML(XmlNode node, int tabCount)
			{
				StringBuilder myNodeString = new();
				myNodeString.Append('\t', tabCount);
				myNodeString.Append($"<{node.Name}");
				// attribs (wrapping when necessary
				int charCount = 0;
				foreach (XmlAttribute attrib in node.Attributes)
				{
					string toAppend = "";
					if (charCount + node.Name.Length >= XML_WRAP_CHARACTER_COUNT)
					{
						charCount = 0;
						myNodeString.Append("\r\n");
						myNodeString.Append('\t', tabCount);
						myNodeString.Append(' ', 12); //idk alright? this is just how studio one does (or did) it
						if (node.Name == "AudioPartClip")
						{
							myNodeString.Append(' ', 3); // this is not maintainable ;)
						}
					}
					else
					{
						toAppend = " ";
					}
					toAppend += $"{attrib.Name}=\"{attrib.Value.Replace("&", "&amp;")}\"";
					charCount += toAppend.Length;
					myNodeString.Append(toAppend);
				}
				if (node.ChildNodes.Count == 0)
				{
					myNodeString.Append("/>\r\n");
				}
				else
				{
					myNodeString.Append(">\r\n");
					foreach (XmlNode xmlNode in node.ChildNodes)
					{
						myNodeString.Append(GetInnerXML(xmlNode, tabCount + 1));
					}
					myNodeString.Append('\t', tabCount);
					myNodeString.Append($"</{node.Name}>\r\n");
				}
				return myNodeString.ToString();
			}
			int tabCount = 0;
			return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" + GetInnerXML(doc.DocumentElement, tabCount);
		}

		private void RestoreFileIfExists(string songFolderPath)
		{
			var backupFiles = Directory.GetFiles(songFolderPath, $"*{BACKUP_FILE_EXTENSION}").ToList();
			var songFiles = Directory.GetFiles(songFolderPath, $"*.song").ToList();
			foreach (var backup in backupFiles)
			{
				var fileName = backup.Replace(BACKUP_FILE_EXTENSION, "");
				if (songFiles.Contains(fileName))
				{
					File.Delete(fileName);
				}
				File.Move(backup, fileName);
				_filesRestored.Add(fileName);
			}
		}
		public async void RestoreBackups(List<string> projectDirectories, CallbackAlert handler, Callback output, CallbackPrompt verifyContinue)
		{
			if (CurrentlyRunning)
			{
				handler("The file updater is already running!", "Holup");
				return;
			}
			ValidatePaths(projectDirectories);
			StringBuilder foldersToCheck = new StringBuilder();
			projectDirectories.Select(x => GetFileName(x, Path.DirectorySeparatorChar)).ToList().ForEach(x => foldersToCheck.AppendLine(x));
			// prompt the user
			if(!await verifyContinue("Continue?", $"Are you sure you want to continue? All your songs that have a file finder backup will be restored to how they were before I was in their life.\n\nSong folders/directories to restore:\n{foldersToCheck}", "Yes", "No"))
			{
				return;
			}
			CurrentlyRunning = true;

			_clearConsole();
			InitBackupVars();
			_currentHandler = handler;
			_currentOutput = output;
			try
			{
				DoStuffWithSongsInThisDir(projectDirectories, RestoreFileIfExists);
			}
			catch (Exception ex)
			{
				_currentHandler($"A problem occured while attempting to restore your backups:\n{ex.Message}", "Fail");
			}

			handler($"{_filesRestored.Count} files have been restored.", "Restore Complete");
			CurrentlyRunning = false;
		}
		private void DeleteFileIfExists(string songFolderPath)
		{
			var backupFiles = Directory.GetFiles(songFolderPath, $"*{BACKUP_FILE_EXTENSION}").ToList();
			foreach (var backup in backupFiles)
			{
				File.Delete(backup);
				_backupsDeleted.Add(backup);
			}
		}
		public async void DeleteBackups(List<string> projectDirectories, CallbackAlert handler, Callback output, CallbackPrompt verifyContinue)
		{
			if (CurrentlyRunning)
			{
				handler("The file updater is already running!", "Holup");
				return;
			}
			ValidatePaths(projectDirectories);
			StringBuilder foldersToCheck = new StringBuilder();
			projectDirectories.Select(x => GetFileName(x, Path.DirectorySeparatorChar)).ToList().ForEach(x => foldersToCheck.AppendLine(x));
			// prompt the user
			if (!await verifyContinue("Are you sure?", $"Are you absolutely positive you want to continue? All your selected songs will delete their file finder backup (any file ending in \"{BACKUP_FILE_EXTENSION}\". " +
				$"There is no going back from here (your only hope of restoring would be having to use a Studio One autosave IF it even exists).\n\nSong folders/directories to check:\n{foldersToCheck}", "Yes", "No"))
			{
				return;
			}
			CurrentlyRunning = true;

			_clearConsole();
			InitBackupVars();
			_currentHandler = handler;
			_currentOutput = output;
			try
			{
				DoStuffWithSongsInThisDir(projectDirectories, DeleteFileIfExists);
			}
			catch (Exception ex)
			{
				_currentHandler($"A problem occured while attempting to delete your backups:\n{ex.Message}", "Fail");
			}

			handler($"{_backupsDeleted.Count} files have been deleted.", "Deletion Complete");
			CurrentlyRunning = false;
		}
	}
}
