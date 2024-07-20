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

		public FilePreferencesViewModel()
		{
			ReplaceSampleOne = true;

			CanSubmit = false;
			SampleFolders = new();
			ProjectFolders = new();
			SampleFolders.CollectionChanged += new NotifyCollectionChangedEventHandler(FoldersCollectionChanged);
			ProjectFolders.CollectionChanged += new NotifyCollectionChangedEventHandler(FoldersCollectionChanged);
			SampleFolders.Add(new FolderInfo(string.Empty, 1));
			ProjectFolders.Add(new FolderInfo(string.Empty, 1));
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
		}
		private void SetCanSubmit()
		{
			foreach (var folder in SampleFolders)
			{
				if (!folder.PathIsValid)
				{
					CanSubmit = false;
					return;
				}
			}
			foreach (var folder in ProjectFolders)
			{
				if (!folder.PathIsValid)
				{
					CanSubmit = false;
					return;
				}
			}
			CanSubmit = true;
		}
	}
	public class FolderInfo : INotifyPropertyChanged
	{
		public bool PathIsValid = false;
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
				_folderPath = value;
				OnPropertyChanged();
			}
		}

		public FolderInfo(string path, int indexInCollection)
		{
			FolderPath = path;
			IndexInCollectionPlusOne = indexInCollection;
		}

		public void VerifyPath()
		{
			PathIsValid = System.IO.Directory.Exists(FolderPath);
		}
	}
}
