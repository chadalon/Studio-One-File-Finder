using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using ReactiveUI;
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
	static class InstrumentEntries
	{
		public const string FX_EXTENSTION = ".fxpreset";
		public const string MEDIA_POOL = "Song/mediapool.xml";
		public const string INSTRUMENT_TAG = "AudioEffectPreset";

		private static Dictionary<FileType, uint> _sampleCounts;
		public static Dictionary<FileType, uint> SampleCounts => _sampleCounts;
		public static void AddToCount(FileType fType, uint addition=1)
		{
			_sampleCounts[fType] += addition;
		}
		public static void Reset(List<FileType> typesToUpdate)
		{
			_sampleCounts = new();
			foreach (var type in typesToUpdate)
			{
				_sampleCounts[type] = 0;
			}
		}
		public static Dictionary<FileType, List<string>> SearchNodes = new()
		{
			{FileType.MediaPool,  new(){"//AudioClip/Url" } },
			{FileType.SampleOne,  new() { "//Zone/Attributes", "//List/Url" }},
			{FileType.Impact, new() { "//List/Url", "//Zone/Attributes" }}
		};
		public static Dictionary<string, FileType> InstrumentCids = new()
		{
			{ "{C37BC9D1-6BD1-46A7-A60C-B13438666448}", FileType.SampleOne },
			{ "{3713E26C-2FCA-4024-9F25-17E9D2BE2B9B}", FileType.Impact }
		};
	}
	class FileUpdater
	{
		const int XML_WRAP_CHARACTER_COUNT = 100;
		public const string BACKUP_FILE_EXTENSION = ".s1filefinderbackup";
		List<string> SUPPORTED_FILE_TYPES = new() { ".wav", ".aiff", ".aif", ".rex", ".caf", ".ogg", ".flac", ".mp3" };
		private const string LOCATING_SAMPLES_STRING = "Locating samples...";
		private const string USER_CANCEL_STRING = "User canceled operation.";

		private bool _currentlyRunning;
		public bool CurrentlyRunning
		{
			get => _currentlyRunning;
			set
			{
				if (_currentlyRunning == value) return;
				_runningCallback?.Invoke(value);
				_currentlyRunning = value;
			}
		}

		private CancellationToken _cancellationToken;

		public delegate void Callback(string message);
		public delegate Task CallbackAlert(string message, string title="Alert");
		public delegate Task<bool> CallbackPrompt(string title, string message, string yes, string no);
		CallbackAlert _currentHandler; // TODO move these into the constructor and stop passing them in through delete, restore, etc.
		Callback _currentOutput;
		BasicDelegate _clearConsole;// TODO move ClearConsole out of fpvm into here
		DoubleCallback _setProgressBar;
		StringCallback _setCurSong;
		BoolCallback _runningCallback;

		private string _currentSongFolderName;

		uint _refUpdateCount;
		uint _projectsUpdated;
		private List<string> _songsUpdated;
		private List<string> _songsSkipped;
		int count; // TODO rename
		private Dictionary<string, string?> _discoveredFiles;
		private List<string> _sampleFolders;
		private ExtraSettings _userConfig;
		private List<string> _filesRestored;
		private List<string> _backupsDeleted;

		private DateTime _startTime;
		public FileUpdater(BasicDelegate clearConsole, DoubleCallback setProgress, StringCallback setCurSong, BoolCallback runningCallback)
		{
			CurrentlyRunning = false;
			_clearConsole = clearConsole;
			_setProgressBar = setProgress;
			_setCurSong = setCurSong;
			_runningCallback = runningCallback;
			InitClass();
		}
		private void InitClass()
		{
			_refUpdateCount = 0;
			_projectsUpdated = 0;
			_songsUpdated = new();
			_songsSkipped = new();
			_sampleFolders = new();
			_discoveredFiles = new();
			InitBackupVars();
		}
		private void InitBackupVars()
		{
			_filesRestored = new();
			_backupsDeleted = new();
		}
		private void StartRunning(CancellationToken cancellationToken)
		{
			CurrentlyRunning = true;
			_cancellationToken = cancellationToken;
			_startTime = DateTime.Now;
			_setProgressBar(0.0);
			_setCurSong("");
			_clearConsole();
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
		private void DoStuffWithSongsInThisDir(List<string> projectDirectories, Callback modifier, bool includeBackups=false)
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
			void FindBackupFolders(DirectoryInfo currentDir)
			{
				var songFiles = Directory.GetFiles(currentDir.FullName, $"*{BACKUP_FILE_EXTENSION}").ToList();
				if (songFiles.Count > 0 && currentDir.Name != "History") // if we excluding autosaves
				{
					songFolders.Add(currentDir.FullName);
				}
				foreach (var item in currentDir.EnumerateDirectories())
				{
					FindBackupFolders(item);
				}

			}

			foreach (string projectsDir in projectDirectories)
			{
				int countBefore = songFolders.Count;
				FindSongFolders(new DirectoryInfo(projectsDir));
				if (includeBackups)
				{
					FindBackupFolders(new DirectoryInfo(projectsDir));
				}

				if (songFolders.Count - countBefore == 0)
				{
					_currentOutput($"No song folders found in {projectsDir}");
					continue;
				}
			}
			foreach (string songFolderPath in songFolders)
			{
				_setCurSong(GetFileName(songFolderPath, Path.DirectorySeparatorChar));
				modifier(songFolderPath);
				if (_cancellationToken.IsCancellationRequested) return;
				count++;
				_setProgressBar((double)count / (double)songFolders.Count);
			}
		}
		public async void UpdateFiles(CancellationToken cancellationToken, List<string> sampleDirectories, List<string> projectDirectories, List<FileType> typesToUpdate, ExtraSettings config, CallbackAlert handler, Callback output)
		{
			if (CurrentlyRunning)
			{
				await handler("The file updater is already running!", "Holup");
				return;
			}
			StartRunning(cancellationToken);

			InitClass();
			InstrumentEntries.Reset(typesToUpdate);
			_userConfig = config;

			_currentHandler = handler;
			_currentOutput = output;
			ValidatePaths(sampleDirectories);
			ValidatePaths(projectDirectories);

			_sampleFolders = sampleDirectories;

			if (sampleDirectories.Count == 0)
			{
				await handler("There are no valid sample directories.");
				return;
			}
			if (projectDirectories.Count == 0)
			{
				await handler("There are no valid project directories.");
				return;
			}
			CacheAllSamples();
			if (_cancellationToken.IsCancellationRequested)
			{
				_currentOutput(USER_CANCEL_STRING);
				CurrentlyRunning = false;
				return;
			}
			DoStuffWithSongsInThisDir(projectDirectories, UpdateSong);
			if (_cancellationToken.IsCancellationRequested)
			{
				_currentOutput(USER_CANCEL_STRING);
			}
			string finalString = $"Updated {_refUpdateCount} sample references ({_projectsUpdated} songs)";
			foreach (var fTypeCount in InstrumentEntries.SampleCounts)
			{
				finalString += $"\n{fTypeCount.Value} of those were {fTypeCount.Key} updates";
			}
			finalString += $"\n\n{_songsSkipped.Count} songs were not updated.";
			finalString += $"\nTime taken: {DateTime.Now - _startTime}";
			await handler(finalString, "Results");
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
				if (_cancellationToken.IsCancellationRequested) return;
			}
		}
		/// <summary>
		/// Write out the entry
		/// </summary>
		/// <param name="destination"></param>
		/// <param name="entry"></param>
		/// <param name="fType"></param>
		private void WriteEntryIfNeeded(ZipArchive destination, ZipArchiveEntry entry, bool isMediaPool=false)
		{
			string alterFileResult;
			Tuple<string, FileType, uint>? result;
			using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
			{
				result = AlterFileIfValidInstrument(reader, isMediaPool);
			}
			if (result == null)
			{
				// keep our hands off the entry if we can
				CopyEntry(destination, entry);
				return;
			}
			alterFileResult = result.Item1;
			var fType = result.Item2;

			var destinationEntry = destination.CreateEntry(entry.FullName);
			using (var writer = new StreamWriter(destinationEntry.Open(), Encoding.UTF8))
			{
				writer.Write(alterFileResult);
			}
			InstrumentEntries.SampleCounts[fType] += result.Item3;
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
			_currentOutput("");
			_currentOutput("");
			_currentOutput($"Discovering samples for {sourceFilePath}");
			_currentOutput("");
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
					if (entry.FullName == InstrumentEntries.MEDIA_POOL && InstrumentEntries.SampleCounts.ContainsKey(FileType.MediaPool))
					{
						WriteEntryIfNeeded(destination, entry, true);
					}
					else if (entry.FullName.StartsWith("Presets/Synths/") && entry.Name.Contains(InstrumentEntries.FX_EXTENSTION))
					{
						WriteEntryIfNeeded(destination, entry);
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
				_currentOutput($"Didn't update {GetFileName(sourceFilePath, Path.DirectorySeparatorChar)}");
				_songsSkipped.Add(sourceFilePath);
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
				_songsUpdated.Add(sourceFilePath);
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
		FileType? CheckForInstrument(XmlDocument doc)
		{
			XmlNodeList? elements = doc.SelectNodes(InstrumentEntries.INSTRUMENT_TAG);
			if (elements == null || elements.Count == 0) return null;
			// attrib
			string? cid = null;
			foreach (XmlNode element in elements)
			{
				cid = element.Attributes?.GetNamedItem("cid")?.Value;
				if (cid != null)
				{
					FileType fType;
					if (InstrumentEntries.InstrumentCids.TryGetValue(cid, out fType))
					{
						return fType;
					}
				}
			}
			return null;
		}
		Tuple<string, FileType, uint>? AlterFileIfValidInstrument(StreamReader fileData, bool isMediaPool)
		{
			XmlReaderSettings settings = new XmlReaderSettings { NameTable = new NameTable() };
			XmlNamespaceManager xmlns = new XmlNamespaceManager(settings.NameTable);
			xmlns.AddNamespace("x", "peepeepoopoo");
			XmlParserContext context = new XmlParserContext(null, xmlns, "", XmlSpace.Default);
			XmlReader reader = XmlReader.Create(fileData, settings, context);
			XmlDocument xmlDoc = new();
			xmlDoc.Load(reader);
			FileType? fileType;
			if (!isMediaPool)
			{
				fileType = CheckForInstrument(xmlDoc);
				if (fileType == null || !InstrumentEntries.SampleCounts.ContainsKey((FileType)fileType))
				{
					return null;
				}
			}
			else
			{
				fileType = FileType.MediaPool;
			}
			List<XmlNodeList> elements = new();
			foreach (string nodes in InstrumentEntries.SearchNodes[(FileType)fileType])
			{
				XmlNodeList? nodeList = xmlDoc.SelectNodes(nodes);
				if (nodeList == null) continue;
				elements.Add(nodeList);
			} 
			uint countBeforeCurEntry = _refUpdateCount;
			if (elements.Count != 0)
			{
				foreach (var elems in elements)
				{
					UpdateXmlNodes(elems);
				}
			}
			if (_refUpdateCount - countBeforeCurEntry == 0) return null;

			return Tuple.Create(Beautify(xmlDoc), (FileType)fileType, _refUpdateCount - countBeforeCurEntry);
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
			if (!_discoveredFiles.TryGetValue(fileName, out matchingFile))
			{
				if (SUPPORTED_FILE_TYPES.Contains(Path.GetExtension(fileName).ToLower())) // it's def not in the sample folders.
				{
					return null;
				}
				foreach (var path in _sampleFolders)
				{
					matchingFile = SearchMyDirOfficer(new DirectoryInfo(path), fileName);
					if (matchingFile != null) break;
				}
				_discoveredFiles[fileName] = matchingFile; // cache to make our other file searches a billion times faster
			}
			return matchingFile;
		}
		void SearchAllSampleDirs(DirectoryInfo currentDir)
		{
			Queue<DirectoryInfo> dirsToSearch = new Queue<DirectoryInfo>();
			dirsToSearch.Enqueue(currentDir);
			while (dirsToSearch.Any())
			{
				if (_cancellationToken.IsCancellationRequested) return;
				DirectoryInfo curDir = dirsToSearch.Dequeue();
				List<FileInfo> files;
				try
				{
					files = curDir.EnumerateFiles().ToList();
				}
				catch (Exception ex)
				{
					// We probably just don't have permissions.
					//add to an error log
					_currentOutput($"Problem with searching folder for samples:\n{ex.Message} - (maybe you want to run this in admin mode?)");
					continue;
				}
				var audioFiles = files.Where(x => SUPPORTED_FILE_TYPES.Contains(Path.GetExtension(x.Name).ToLower())).ToList();
				if (audioFiles.Count > 0)
				{
					foreach (var audioFile in audioFiles)
					{
						_discoveredFiles[audioFile.Name] = audioFile.FullName;
						if (_discoveredFiles.Count % 10 == 0)
						{
							_setCurSong($"{LOCATING_SAMPLES_STRING} {_discoveredFiles.Count} discovered");
						}
					}
				}

				var dirs = curDir.EnumerateDirectories();
				foreach (var dir in dirs)
				{
					dirsToSearch.Enqueue(dir);
				}

			}
		}
		private void CacheAllSamples()
		{
			_currentOutput("Finding all audio files...");
			_setCurSong(LOCATING_SAMPLES_STRING);
			foreach (var path in _sampleFolders)
			{
				SearchAllSampleDirs(new DirectoryInfo(path));
				if (_cancellationToken.IsCancellationRequested ) return;
			}
			_currentOutput($"Found {_discoveredFiles.Count} samples");
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
							_currentOutput($"{fileName} reference is good.");
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
				_currentOutput($"Restored {fileName}");
				_filesRestored.Add(fileName);
			}
		}
		public async void RestoreBackups(CancellationToken cancellationToken, List<string> projectDirectories, CallbackAlert handler, Callback output, CallbackPrompt verifyContinue)
		{
			if (CurrentlyRunning)
			{
				await handler("The file updater is already running!", "Holup");
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
			StartRunning(cancellationToken);
			InitBackupVars();
			_currentHandler = handler;
			_currentOutput = output;
			try
			{
				DoStuffWithSongsInThisDir(projectDirectories, RestoreFileIfExists, true);
			}
			catch (Exception ex)
			{
				await _currentHandler($"A problem occured while attempting to restore your backups:\n{ex.Message}", "Fail");
			}

			await handler($"{_filesRestored.Count} files have been restored.", "Restore Complete");
			CurrentlyRunning = false;
		}
		private void DeleteFileIfExists(string songFolderPath)
		{
			var backupFiles = Directory.GetFiles(songFolderPath, $"*{BACKUP_FILE_EXTENSION}").ToList();
			foreach (var backup in backupFiles)
			{
				File.Delete(backup);
				_currentOutput($"Deleted {backup}");
				_backupsDeleted.Add(backup);
			}
		}
		public async void DeleteBackups(CancellationToken cancellationToken, List<string> projectDirectories, CallbackAlert handler, Callback output, CallbackPrompt verifyContinue)
		{
			if (CurrentlyRunning)
			{
				await handler("The file updater is already running!", "Holup");
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
			StartRunning(cancellationToken);
			InitBackupVars();
			_currentHandler = handler;
			_currentOutput = output;
			try
			{
				DoStuffWithSongsInThisDir(projectDirectories, DeleteFileIfExists);
			}
			catch (Exception ex)
			{
				await _currentHandler($"A problem occured while attempting to delete your backups:\n{ex.Message}", "Fail");
			}

			await handler($"{_backupsDeleted.Count} files have been deleted.", "Deletion Complete");
			CurrentlyRunning = false;
		}
	}
}
