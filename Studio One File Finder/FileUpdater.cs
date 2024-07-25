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
		SampleOne
	}
	class FileUpdater
	{
		const int XML_WRAP_CHARACTER_COUNT = 100;
		const string MEDIA_POOL = "Song/mediapool.xml";
		const string BACKUP_FILE_EXTENSION = ".s1filefinderbackup";
		private static string FILE_TYPE_NODES(FileType fType)
		{
			switch(fType)
			{
				case FileType.MediaPool: return "//AudioClip/Url";
				case FileType.SampleOne: return "//Zone/Attributes";
				default: return null;
			}
		}
		Dictionary<FileType, string> _nodesToFind;

		public bool CurrentlyRunning = false;

		private bool _updateSampleOneRefs;

		public delegate void Callback(string message);
		public delegate void CallbackAlert(string message, string title="Alert");
		CallbackAlert _currentHandler;
		Callback _currentOutput;

		uint _refUpdateCount;
		uint _projectsUpdated;
		int count; // TODO rename
		private Dictionary<string, string?> _discoveredFiles;
		private List<string> _sampleFolders;
		private ExtraSettings _userConfig;
		public FileUpdater()
		{
			InitClass(true);
		}
		private void InitClass(bool resetCachedPaths=false)
		{
			_refUpdateCount = 0;
			_projectsUpdated = 0;
			count = 0;
			_sampleFolders = new();
			_nodesToFind = new()
			{
				{ FileType.MediaPool, FILE_TYPE_NODES(FileType.MediaPool) }
			};
			if (resetCachedPaths) _discoveredFiles = new();
		}
		private void ValidatePaths(List<string> dirs, Callback handler)
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
					handler($"Invalid directory: {dirs[i]}");
					dirs.RemoveAt(i);
				}
			}
		}
		public void UpdateFiles(List<string> sampleDirectories, List<string> projectDirectories, List<FileType> typesToUpdate, ExtraSettings config, CallbackAlert handler, Callback output)
		{
			if (CurrentlyRunning)
			{
				_currentHandler("The file updater is already running!", "Holup");
				return;
			}
			CurrentlyRunning = true;

			InitClass();
			foreach (var fType in typesToUpdate)
			{
				_nodesToFind[fType] = FILE_TYPE_NODES(fType);
			}
			_userConfig = config;

			_currentHandler = handler;
			_currentOutput = output;
			ValidatePaths(sampleDirectories, output);
			ValidatePaths(projectDirectories, output);

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

			foreach (string projectsDir in projectDirectories)
			{

				var songFolders = Directory.GetDirectories(projectsDir);
				if (songFolders.Length == 0)
				{
					output($"No song folders found in {projectsDir}");
					continue;
				}
				foreach (string songFolderPath in songFolders)
				{
					UpdateSong(songFolderPath);
					count++;
					if (count > 4)
					{
						break;
					}
				}

			}
			handler($"Updated {_refUpdateCount} sample references ({_projectsUpdated} songs)");
			CurrentlyRunning = false;
		}
		private void UpdateSong(string songFolderPath)
		{
			// TODO just selecting first legit song file. Do ppl ever store multiple in their folders??
			var songFile = Directory.GetFiles(songFolderPath, "*.song").Where(x => !Path.GetFileName(x).StartsWith("._")).FirstOrDefault();
			//throw new Exception("need to exclude ._*.song files...");
			if (songFile == null)
			{
				// TODO maybe check autosaves here (could make a setting for this)

				_currentOutput($"No song file found in {songFolderPath} (it may have some autosaves)...skipping to next");
				return;
			}

			LoadProject(songFile);
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
			uint countBeforeCurFile = _refUpdateCount;
			// TODO test inputting both / and \\
			string tempFilePath = sourceFilePath + "temp"; // will this work lmao
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
					else if (entry.FullName.Contains("SampleOne") && _nodesToFind.ContainsKey(FileType.SampleOne))
					{
						_currentOutput("Checking SampleOne files...");
						WriteEntryIfNeeded(destination, entry, FileType.SampleOne);
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
				//File.Delete(sourceFilePath);
				string newBackupPath = sourceFilePath + BACKUP_FILE_EXTENSION;
				if (File.Exists(newBackupPath))
				{
					File.Delete(newBackupPath);
				}

				File.Move(sourceFilePath, newBackupPath);
				File.Move(tempFilePath, sourceFilePath);
				_projectsUpdated++;
			}
		}
		string? SearchMyDirOfficer(DirectoryInfo currentDir, string fileName)
		{
			var triedFile = currentDir.EnumerateFiles().ToList().FirstOrDefault(x => x.Name == fileName);
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
		private string GetFileName(string fullPath)
		{
			string[] dirName = fullPath.Split('/'); // TODO if we are fed a windows path this will be \
			return dirName[dirName.Length - 1];
		}
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
					if (currentFilePath == null) return;
				}
				string fileName = GetFileName(currentFilePath);
				if (!modifiedFileName)
				{
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
	}
}
