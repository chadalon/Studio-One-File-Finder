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
		public event PropertyChangedEventHandler PropertyChanged; 
		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private ObservableCollection<FolderInfo> _projectFolders;
		public ObservableCollection<FolderInfo> ProjectFolders
		{
			get => _projectFolders;
			set
			{
				_projectFolders = value;
				OnPropertyChanged();
			}
		}
		private ObservableCollection<FolderInfo> _sampleFolders;
		public ObservableCollection<FolderInfo> SampleFolders
		{
			get => _sampleFolders;
			set
			{
				_sampleFolders = value;
				OnPropertyChanged();
			}
		}
		private bool _replaceSampleOne;
		public bool ReplaceSampleOne
		{
			get => _replaceSampleOne;
			set
			{
				_replaceSampleOne = value;
				OnPropertyChanged();
			}
		}

		private bool _canSubmit;
		public bool CanSubmit
		{
			get => _canSubmit;
			set
			{
				_canSubmit = value;
				OnPropertyChanged();
			}
		}
		private ReactiveCommand<Unit, Unit> _submitCommand;
		public ReactiveCommand<Unit, Unit> SubmitCommand
		{
			get => _submitCommand;
			set
			{
				_submitCommand = value;
				OnPropertyChanged();
			}
		}

		private string _outputText;
		public string OutputText
		{
			get => _outputText;
			set
			{
				if (OutputText == value) return;
				_outputText = value;
				OnPropertyChanged();
			}
		}

		private FileUpdater _fileUpdater;

		public FilePreferencesViewModel()
		{
			_fileUpdater = new();

			ReplaceSampleOne = true;

			CanSubmit = false;
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
			IObservable<bool> canSubmit = this.WhenAnyValue(
				x => x.CanSubmit);
			SubmitCommand = ReactiveCommand.Create(SubmitEverything, canSubmit);
		}
		private void SubmitEverything()
		{
			List<string> validSampleDirs = SampleFolders.Where(x => x.PathIsValid).Select(x => x.FolderPath).ToList();
			List<string> validProjectDirs = ProjectFolders.Where(x => x.PathIsValid).Select(x => x.FolderPath).ToList();
			List<FileType> extraPlugins = new List<FileType>();
			if (ReplaceSampleOne) // TODO there's a better way to do this with observables, hashtables, etc
				extraPlugins.Add(FileType.SampleOne);
			FileUpdater.Callback errorHandler = (string message) =>
			{
				// error popup
				OutputText = message;
			};
			FileUpdater.Callback outputHandler = (string message) =>
			{
				OutputText = message;
			};

			_fileUpdater.UpdateFiles(validSampleDirs, validProjectDirs, extraPlugins, errorHandler, outputHandler);
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
}
