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
	public delegate void BasicDelegate();
	public delegate void DoubleCallback(double val);
	public delegate void StringCallback(string val);
	public delegate void BoolCallback(bool val);
	public class FilePreferencesViewModel : INotifyPropertyChanged
	{
		public delegate Task MyEventAction(string title, string message, string buttonContent);
		public event MyEventAction Alert;
		public delegate Task<bool> MyPromptEventAction(string title, string message, string yes, string no);
		public event MyPromptEventAction PromptAlert;
		public delegate void ControlMusic(bool play);
		public event MyEventAction Play;

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
		public ReactiveCommand<Unit, Unit> StopCommand { get; set; }

		private string _currentSong;
		public string CurrentSong
		{
			get => _currentSong;
			set
			{
				SetIfDiff(ref _currentSong, value);
			}
		}

		private bool _currentlyRunning;
		public bool CurrentlyRunning
		{
			get => _currentlyRunning;
			set
			{
				SetIfDiff(ref _currentlyRunning, value);
			}
		}

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

		private CancellationTokenSource _cancellationTokenSource;

		private FileUpdater _fileUpdater;

		public FilePreferencesViewModel()
		{
		}
		public void InitializeFilePreferences(MyEventAction alertAction, MyPromptEventAction promptAlertAction)
		{
			_cancellationTokenSource = new CancellationTokenSource();
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
			StringCallback setCurSong = (string val) =>
			{
				CurrentSong = val;
			};
			BoolCallback setCurrentlyRunning = (bool val) =>
			{
				CurrentlyRunning = val;
			};
			_fileUpdater = new(clearConsole, setProgress, setCurSong, setCurrentlyRunning);

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

			//StopCommand = ReactiveCommand.Create(StopFileUpdating);

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
		private CancellationToken GetNewCancellationToken()
		{
			if (!_cancellationTokenSource.Token.IsCancellationRequested)
			{
				return _cancellationTokenSource.Token;
			}
			_cancellationTokenSource = new CancellationTokenSource();
			return _cancellationTokenSource.Token;
		}
		public void StopFileUpdating()
		{
			_cancellationTokenSource.Cancel();
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
				if (message == "")
				{
					OutputText += "\n";
					return;
				}
				DateTime curDate = DateTime.Now;
				string msgToOut = $"\n<{curDate.ToString("HH:mm:ss.fff")}> {message}";
				OutputText += msgToOut;
			};
			var settings = new ExtraSettings
			{
				OverwriteValidPaths = OverWriteValidPaths,
				UpdateDuplicateFiles = UpdateDuplicates
			};
			_fileUpdater.UpdateFiles(GetNewCancellationToken(), validSampleDirs, validProjectDirs, extraPlugins, settings, errorHandler, outputHandler);
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
			_fileUpdater.RestoreBackups(GetNewCancellationToken(), validProjectDirs, errorHandler, outputHandler, askToCont);
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
			_fileUpdater.DeleteBackups(GetNewCancellationToken(), validProjectDirs, errorHandler, outputHandler, askToCont);

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
						Task.Run(folder.VerifyPath);
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
		private bool _isValidating;
		public bool IsValidating
		{
			get => _isValidating;
			set
			{
				_isValidating = value;
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
			IsValidating = false;
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
			PathIsValid = false;
			PathIsValid = await Task.Run(FolderPathIsValid);// FolderPathIsValid();// await new Task<bool>(FolderPathIsValid);
		}
	}
	public class SongFolderInfo : FolderInfo
	{
		public SongFolderInfo(string path, int indexInCollection, FilePreferencesViewModel.MyEventAction alert) : base(path, indexInCollection, alert) { }
		public override void VerifyPath()
		{
			IsValidating = true;
			PathIsValid = false;
			PathIsValid = FolderPathIsValidAsync().Result;
			IsValidating = false;
		}
		private async Task<bool> FolderPathIsValidAsync()
		{
			if (!base.FolderPathIsValid()) return false;
			try
			{
				return SearchMyDirOfficer(new DirectoryInfo(FolderPath));
			}
			catch (Exception ex)
			{
				await Application.Current!.Dispatcher.DispatchAsync(async () => await _alert.Invoke("Problem with directory", ex.Message, "okay bruv"));
				return false;
			}
		}
		/// <summary>
		/// Perform a breadth-first search of directory to find any valid .song file (or song backup)
		/// </summary>
		/// <param name="currentDir"></param>
		/// <returns></returns>
		bool SearchMyDirOfficer(DirectoryInfo dir)
		{
			Queue<DirectoryInfo> directoriesToSearch = new();
			directoriesToSearch.Enqueue(dir);
			while (directoriesToSearch.Any())
			{
				DirectoryInfo currentDir = directoriesToSearch.Dequeue();
				List<string> songFiles;
				List<string> bupFiles;
				try
				{
					songFiles = Directory.GetFiles(currentDir.FullName, $"*.song").ToList();
					bupFiles = Directory.GetFiles(currentDir.FullName, $"*{FileUpdater.BACKUP_FILE_EXTENSION}").ToList();
				}
				catch (Exception e)
				{
					// probs just don't have admin rights to current folder
					continue;
				}
				if (songFiles.Count > 0 || bupFiles.Count > 0) return true;
				foreach (var item in currentDir.EnumerateDirectories())
				{
					directoriesToSearch.Enqueue(item);
				}
			}
			return false;
		}
	}

	public struct ExtraSettings
	{
		public bool OverwriteValidPaths;
		public bool UpdateDuplicateFiles;
		List<FileType> ExtraPlugins;

	}
}
