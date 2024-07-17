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

		public void AddNewSampleFolder()
		{
			SampleFolders.Add(new FolderInfo(string.Empty));
		}

		public FilePreferencesViewModel()
		{
			FolderPath = "C:\\";

			SampleFolders = new ObservableCollection<FolderInfo>
			{
				new FolderInfo(string.Empty)
			};
		}
	}
	public class FolderInfo : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

		public FolderInfo(string path)
		{
			FolderPath = path;
		}

		public bool VerifyPath()
		{
			return System.IO.Directory.Exists(FolderPath);
		}
	}
}
