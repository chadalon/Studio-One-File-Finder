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
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Studio_One_File_Finder
{
	public class FilePreferencesViewModel : INotifyPropertyChanged
	{
		public delegate void MyEventAction(string title, string message, string buttonContent);
		public event MyEventAction Alert;
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
		private bool _replaceSampleOne;
		public bool ReplaceSampleOne
		{
			get => _replaceSampleOne;
			set
			{
				SetIfDiff(ref _replaceSampleOne, value);
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

		private FileUpdater _fileUpdater;

		public FilePreferencesViewModel()
		{
			_fileUpdater = new();

			ReplaceSampleOne = true;
			OverWriteValidPaths = false;
			UpdateDuplicates = false;

			CanSubmit = false;
			IsMusicPlaying = true;
			SampleFolders = new();
			ProjectFolders = new();
			SampleFolders.CollectionChanged += new NotifyCollectionChangedEventHandler(FoldersCollectionChanged);
			ProjectFolders.CollectionChanged += new NotifyCollectionChangedEventHandler(FoldersCollectionChanged);
			SampleFolders.Add(new FolderInfo(string.Empty, 1));
			ProjectFolders.Add(new FolderInfo(string.Empty, 1));
			OutputText = "Hello, World!";

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
			if (ReplaceSampleOne) // TODO there's a better way to do this with observables, hashtables, etc
				extraPlugins.Add(FileType.SampleOne);
			FileUpdater.CallbackAlert errorHandler = async (string message, string title) =>
			{
				// error popup
				Alert?.Invoke(title, message, "okay bruv");
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

		public void AddNewSampleFolder()
		{
			SampleFolders.Add(new FolderInfo(string.Empty, SampleFolders.Count + 1));
		}
		public void AddNewProjectFolder()
		{
			ProjectFolders.Add(new FolderInfo(string.Empty, ProjectFolders.Count + 1));
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
					(ob as FolderInfo).PropertyChanged += (sender, e) =>
					{
						(sender as FolderInfo).VerifyPath();
						SetCanSubmit();
					};
				}
			}
			if (e.OldItems != null)
			{

			}
			SetCanSubmit();
		}
		/// <summary>
		/// If we have one valid folder for samlples and one valid for projects, we can submit
		/// </summary>
		private void SetCanSubmit()
		{
			CanSubmit = SampleFolders.Any(x => x.PathIsValid) && ProjectFolders.Any(x => x.PathIsValid);
		}
	}
	public class FolderInfo : INotifyPropertyChanged
	{
		// TODO set if changed func
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

		public FolderInfo(string path, int indexInCollection)
		{
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

		public void VerifyPath()
		{
			PathIsValid = System.IO.Directory.Exists(FolderPath);
		}
	}
	public struct ExtraSettings
	{
		public bool OverwriteValidPaths;
		public bool UpdateDuplicateFiles;
		List<FileType> ExtraPlugins;

	}
}
