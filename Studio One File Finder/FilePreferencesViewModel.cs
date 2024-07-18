using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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

		public FilePreferencesViewModel()
		{
			SampleFolders = new ObservableCollection<FolderInfo>
			{
				new FolderInfo(string.Empty, 0)
			};
			ProjectFolders = new ObservableCollection<FolderInfo>
			{
				new FolderInfo(string.Empty, 0)
			};
			ReplaceSampleOne = true;
		}
	}
	public class FolderInfo : INotifyPropertyChanged
	{
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

		public bool VerifyPath()
		{
			return System.IO.Directory.Exists(FolderPath);
		}
	}
}
