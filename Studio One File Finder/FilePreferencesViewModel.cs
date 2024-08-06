using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;
using DynamicData.Binding;
using System.Collections.Specialized;

namespace Studio_One_File_Finder
{
	public class FilePreferencesViewModel : INotifyPropertyChanged
	{
		public delegate Task MyEventAction(string title, string message, string buttonContent);
		public event MyEventAction Alert;
		public delegate Task<bool> MyPromptEventAction(string title, string message, string yes, string no);
		public event MyPromptEventAction PromptAlert;
		public delegate void ControlMusic(bool play);
		public event MyEventAction Play;
		public delegate void BasicDelegate();
		public delegate void DoubleCallback(double val);

		private void SetIfDiff<T>(ref T curVal, T newVal, [CallerMemberName] string propertyName = null)
		{
			if (curVal != null && curVal.Equals(newVal) || curVal == null && newVal == null) return;
			curVal = newVal;
			OnPropertyChanged(propertyName);
		}
		public event PropertyChangedEventHandler PropertyChanged; 
		protected void OnPropertyChanged(string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private ObservableCollection<FolderInfo> _projectFolders;
		public ObservableCollection<FolderInfo> ProjectFolders
		{
			get => _projectFolders;
			set
			{
				SetIfDiff(ref _projectFolders, value);
			}
		}
		private ObservableCollection<FolderInfo> _sampleFolders;
		public ObservableCollection<FolderInfo> SampleFolders
		{
			get => _sampleFolders;
			set
			{
				SetIfDiff(ref _sampleFolders, value);
			}
		}
		private bool _replaceMediaPool;
		public bool ReplaceMediaPool
		{
			get => _replaceMediaPool;
			set
			{
				SetIfDiff(ref _replaceMediaPool, value);
			}
		}
		private bool _replaceSampleOne;
		public bool ReplaceSampleOne
		{
			get => _replaceSampleOne;
			set
			{
				SetIfDiff(ref _replaceSampleOne, value);
			}
		}
		private bool _replaceImpact;
		public bool ReplaceImpact
		{
			get => _replaceImpact;
			set
			{
				SetIfDiff(ref _replaceImpact, value);
			}
		}
		private bool _overWriteValidPaths;
		public bool OverWriteValidPaths
		{
			get => _overWriteValidPaths;
			set
			{
				SetIfDiff(ref _overWriteValidPaths, value);
			}
		}
		private bool _updateDuplicates;
		public bool UpdateDuplicates
		{
			get => _updateDuplicates;
			set
			{
				SetIfDiff(ref _updateDuplicates, value);
			}
		}

		private bool _canSubmit;
		public bool CanSubmit
		{
			get => _canSubmit;
			set
			{
				SetIfDiff(ref _canSubmit, value);
			}
		}

		private bool _canRestore;
		public bool CanRestore
		{
			get => _canRestore;
			set
			{
				SetIfDiff(ref _canRestore, value);
			}
		}

		private bool _isMusicPlaying;
		public bool IsMusicPlaying
		{
			get => _isMusicPlaying;
			set
			{
				SetIfDiff(ref _isMusicPlaying, value);
			}
		}
		public ReactiveCommand<Unit, Unit> SubmitCommand { get; }

		private string _outputText;
		public string OutputText
		{
			get => _outputText;
			set
			{
				SetIfDiff(ref _outputText, value);
			}
		}

		private double _progressBarValue;
		public double ProgressBarValue
		{
			get => _progressBarValue;
			set
			{
				SetIfDiff(ref _progressBarValue, value);
			}
		}

		private FileUpdater _fileUpdater;

		public FilePreferencesViewModel()
		{
		}
		public void InitializeFilePreferences(MyEventAction alertAction, MyPromptEventAction promptAlertAction)
		{
			Alert += alertAction;
			PromptAlert += promptAlertAction;
			OutputText = "";
			ProgressBarValue = 0.0;
			BasicDelegate clearConsole = () =>
			{
				OutputText = "";
			};
			DoubleCallback setProgress = (double val) =>
			{
				ProgressBarValue = val;
			};
			_fileUpdater = new(clearConsole, setProgress);

			ReplaceMediaPool = true;
			ReplaceSampleOne = true;
			ReplaceImpact = true;
			OverWriteValidPaths = false;
			UpdateDuplicates = false;

			CanSubmit = false;
			CanRestore = false;
			IsMusicPlaying = true;
			SampleFolders = new();
			ProjectFolders = new();
			SampleFolders.CollectionChanged += new NotifyCollectionChangedEventHandler(FoldersCollectionChanged);
			ProjectFolders.CollectionChanged += new NotifyCollectionChangedEventHandler(FoldersCollectionChanged);
			AddNewSampleFolder();
			AddNewProjectFolder();
			OutputText = "Hello, World!";
			this.WhenAnyValue(x => x.ReplaceMediaPool, x => x.ReplaceSampleOne, x => x.ReplaceImpact).Subscribe(_ => SetCanSubmit());

			/*
			ProjectFolders.ToObservableChangeSet().Subscribe(_ =>
			{
				SubmitEverything();
			});*/
			/*
			IObservable<bool> canSubmit = this.WhenAnyValue(
				x => x.CanSubmit);
			SubmitCommand = ReactiveCommand.Create(() =>
			{
				try
				{
					SubmitEverything();
				}
				catch (Exception e)
				{
					OutputText = e.Message;
				}
			});//, canSubmit);*/
		}
		public void SubmitEverything()
		{
			List<string> validSampleDirs = SampleFolders.Where(x => x.PathIsValid).Select(x => x.FolderPath).ToList();
			List<string> validProjectDirs = ProjectFolders.Where(x => x.PathIsValid).Select(x => x.FolderPath).ToList();
			List<FileType> extraPlugins = new List<FileType>();
			if (ReplaceMediaPool)
				extraPlugins.Add(FileType.MediaPool);
			if (ReplaceSampleOne) // TODO there's a better way to do this with observables, hashtables, etc
				extraPlugins.Add(FileType.SampleOne);
			if (ReplaceImpact)
				extraPlugins.Add(FileType.Impact);
			FileUpdater.CallbackAlert errorHandler = async (string message, string title) =>
			{
				// error popup
				await Application.Current.Dispatcher.DispatchAsync(async () => await Alert.Invoke(title, message, "okay bruv"));
			};
			FileUpdater.Callback outputHandler = (string message) =>
			{
				DateTime curDate = DateTime.Now;
				string msgToOut = $"\n<{curDate.ToString("HH:mm:ss.fff")}> {message}";
				OutputText += msgToOut;
			};
			var settings = new ExtraSettings
			{
				OverwriteValidPaths = OverWriteValidPaths,
				UpdateDuplicateFiles = UpdateDuplicates
			};
			_fileUpdater.UpdateFiles(validSampleDirs, validProjectDirs, extraPlugins, settings, errorHandler, outputHandler);
		}
		public void RestoreFiles()
		{
			List<string> validProjectDirs = ProjectFolders.Where(x => x.PathIsValid).Select(x => x.FolderPath).ToList();

			FileUpdater.CallbackAlert errorHandler = async (string message, string title) =>
			{
				// error popup
				await Application.Current.Dispatcher.DispatchAsync(async () => await Alert.Invoke(title, message, "okay bruv"));
			};
			FileUpdater.Callback outputHandler = (string message) =>
			{
				DateTime curDate = DateTime.Now;
				string msgToOut = $"\n<{curDate.ToString("HH:mm:ss.fff")}> {message}";
				OutputText += msgToOut;
			};
			FileUpdater.CallbackPrompt askToCont = async (string title, string message, string yes, string no) =>
			{
				return await Application.Current.Dispatcher.DispatchAsync(async () => await PromptAlert(title, message, yes, no));
			};
			_fileUpdater.RestoreBackups(validProjectDirs, errorHandler, outputHandler, askToCont);
		}
		public void DeleteBackups()
		{
			List<string> validProjectDirs = ProjectFolders.Where(x => x.PathIsValid).Select(x => x.FolderPath).ToList();

			FileUpdater.CallbackAlert errorHandler = async (string message, string title) =>
			{
				// error popup
				await Application.Current.Dispatcher.DispatchAsync(async () => await Alert.Invoke(title, message, "okay bruv"));
			};
			FileUpdater.Callback outputHandler = (string message) =>
			{
				DateTime curDate = DateTime.Now;
				string msgToOut = $"\n<{curDate.ToString("HH:mm:ss.fff")}> {message}";
				OutputText += msgToOut;
			};
			FileUpdater.CallbackPrompt askToCont = async (string title, string message, string yes, string no) =>
			{
				return await Application.Current.Dispatcher.DispatchAsync(async () => await PromptAlert(title, message, yes, no));
			};
			_fileUpdater.DeleteBackups(validProjectDirs, errorHandler, outputHandler, askToCont);

		}

		public void AddNewSampleFolder()
		{
			SampleFolders.Add(new FolderInfo(string.Empty, SampleFolders.Count + 1, Alert));
		}
		public void AddNewProjectFolder()
		{
			ProjectFolders.Add(new SongFolderInfo(string.Empty, ProjectFolders.Count + 1, Alert));
		}

		public void RemoveFolder(ObservableCollection<FolderInfo> folders, FolderInfo folder)
		{
			folders.Remove(folder);
			for (int i = 0; i < folders.Count; i++)
			{
				folders[i].IndexInCollectionPlusOne = i + 1;
			}
		}
		private void FoldersCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.NewItems != null)
			{
				foreach (var ob in e.NewItems)
				{
					var folder = (FolderInfo)ob;
					var folderVerifDisposable = folder.WhenAnyValue(x => x.FolderPath).Subscribe(folderPath =>
					{
						folder.VerifyPath();
					});
					folder.FolderDisposables.Add(folderVerifDisposable);
					var folderValidDisposable = folder.WhenAnyValue(x => x.PathIsValid).Subscribe(pathIsValid =>
					{
						SetCanSubmit();
						if (folder is SongFolderInfo)
						{
							SetCanRestore();
						}
					});
					folder.FolderDisposables.Add(folderValidDisposable);
				}
			}
			if (e.OldItems != null)
			{
				foreach (var ob in e.OldItems)
				{
					var folder = (FolderInfo)ob;
					foreach (var disposable in folder.FolderDisposables)
					{
						disposable.Dispose();
					}
					SetCanSubmit();
					if (folder is SongFolderInfo)
					{
						SetCanRestore();
					}
				}
			}
		}
		/// <summary>
		/// If we have one valid folder for samlples and one valid for projects, we can submit
		/// </summary>
		private void SetCanSubmit()
		{
			CanSubmit = SampleFolders.Any(x => x.PathIsValid) && ProjectFolders.Any(x => x.PathIsValid) && (ReplaceMediaPool || ReplaceSampleOne || ReplaceImpact);
		}
		private void SetCanRestore()
		{
			CanRestore = ProjectFolders.Any(x => x.PathIsValid);
		}
	}
	public class FolderInfo : INotifyPropertyChanged
	{
		public List<IDisposable> FolderDisposables = new();
		private bool _pathIsValid;
		public bool PathIsValid
		{
			get => _pathIsValid;
			set
			{
				if (PathIsValid == value) return;
				_pathIsValid = value;
				OnPropertyChanged();
			}
		}
		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
		private int _indexInCollectionPlusOne;
		public int IndexInCollectionPlusOne
		{
			get => _indexInCollectionPlusOne;
			set
			{
				_indexInCollectionPlusOne = value;
				OnPropertyChanged();
			}
		}
		private string _folderPath;
		public string FolderPath
		{
			get => _folderPath;
			set
			{
				if (FolderPath == value) return;
				_folderPath = value;
				OnPropertyChanged();
			}
		}
		private Color _textColor;
		public Color TextColor
		{
			get => _textColor;
			set
			{
				_textColor = value;
				OnPropertyChanged();
			}
		}

		protected FilePreferencesViewModel.MyEventAction _alert;

		public FolderInfo(string path, int indexInCollection, FilePreferencesViewModel.MyEventAction alert)
		{
			_alert = alert;
			FolderPath = path;
			IndexInCollectionPlusOne = indexInCollection;
			PathIsValid = false;

			// TODO dispose
			this.WhenAnyValue(x => x.PathIsValid).Subscribe(valid =>
			{
				if (valid)
				{
					TextColor = ThemeStuff.GetDefaultTextColor();
				}
				else
				{
					TextColor = ThemeStuff.GetErrorText();
				}
			});
		}
		protected bool FolderPathIsValid()
		{
			return System.IO.Directory.Exists(FolderPath);
		}
		public virtual async void VerifyPath()
		{
			PathIsValid = await Task.Run(FolderPathIsValid);// FolderPathIsValid();// await new Task<bool>(FolderPathIsValid);
		}
	}
	public class SongFolderInfo : FolderInfo
	{
		private Queue<DirectoryInfo> _directoriesToSearch;
		public SongFolderInfo(string path, int indexInCollection, FilePreferencesViewModel.MyEventAction alert) : base(path, indexInCollection, alert) { }
		public override async void VerifyPath()
		{
			PathIsValid = await FolderPathIsValidAsync();
		}
		private async Task<bool> FolderPathIsValidAsync()
		{
			if (!base.FolderPathIsValid()) return false;
			_directoriesToSearch = new Queue<DirectoryInfo>();
			try
			{
				return SearchMyDirOfficer(new DirectoryInfo(FolderPath));
			}
			catch (Exception ex)
			{
				await Application.Current.Dispatcher.DispatchAsync(async () => _alert.Invoke("Problem with directory", ex.Message, "okay bruv"));
				return false;
			}
		}
		/// <summary>
		/// Perform a breadth-first search of directory to find any valid .song file (or song backup)
		/// </summary>
		/// <param name="currentDir"></param>
		/// <param name="depth"></param>
		/// <returns></returns>
		bool SearchMyDirOfficer(DirectoryInfo currentDir)
		{
			var songFiles = Directory.GetFiles(currentDir.FullName, $"*.song").ToList();
			var bupFiles = Directory.GetFiles(currentDir.FullName, $"*{FileUpdater.BACKUP_FILE_EXTENSION}").ToList();
			if (songFiles.Count > 0 || bupFiles.Count > 0) return true;
            foreach (var item in currentDir.EnumerateDirectories())
			{
				_directoriesToSearch.Enqueue(item);
			}
			if (_directoriesToSearch.Count == 0) return false;
			return SearchMyDirOfficer(_directoriesToSearch.Dequeue());
		}
	}

	public struct ExtraSettings
	{
		public bool OverwriteValidPaths;
		public bool UpdateDuplicateFiles;
		List<FileType> ExtraPlugins;

	}
}
