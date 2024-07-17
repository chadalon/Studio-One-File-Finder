using System.Threading.Tasks;

namespace Studio_One_File_Finder
{
	public partial class MainPage : ContentPage
	{
		int count = 0;
		private IFolderPicker _folderPicker;
		public FilePreferencesViewModel FilePreferences;
		public string Hello = "poo";
		public MainPage(IFolderPicker folderPicker, FilePreferencesViewModel filePreferencesViewModel)
		{
			InitializeComponent();
			_folderPicker = folderPicker;
			FilePreferences = filePreferencesViewModel;

			BindingContext = FilePreferences;
		}

		private void OnCounterClicked(object sender, EventArgs e)
		{
			count += 10;

			/*
			if (count == 1)
				CounterBtn.Text = $"Clicked {count} time";
			else
				CounterBtn.Text = $"Clicked {count} times";

			SemanticScreenReader.Announce(CounterBtn.Text);*/
		}
		private async void OnBrowseClicked(object sender, EventArgs e)
		{
			var btn = sender as Button;
			FolderInfo fi = btn.BindingContext as FolderInfo;
			//await Navigation.PushAsync(new BrowsePage());
			var pickedFolder = await PickFolder(new CancellationToken());
			if (pickedFolder != null)
			{
				fi.FolderPath = pickedFolder;
			}
		}
		private async void OnDeleteSampleDirClicked(object sender, EventArgs e)
		{
			var btn = sender as Button;
			FolderInfo fi = btn.BindingContext as FolderInfo;
			FilePreferences.RemoveFolder(FilePreferences.SampleFolders, fi);
		}

		private void OnAddSampleDirClicked(object sender, EventArgs e)
		{
			FilePreferences.AddNewSampleFolder();
		}
		private void OnDeleteProjectDirClicked(object sender, EventArgs e)
		{
			var btn = sender as Button;
			FolderInfo fi = btn.BindingContext as FolderInfo;
			FilePreferences.RemoveFolder(FilePreferences.ProjectFolders, fi);
		}
		private void OnAddProjectDirClicked(object sender, EventArgs e)
		{
			FilePreferences.AddNewProjectFolder();
		}
		async Task<string?> PickFolder(CancellationToken cancellationToken)
		{
			var result = await _folderPicker.PickFolder();
			if (result != null)
			{
				// TODO check if it's a valid path
			}
			return result;
		}

		private void LocationEntry_Unfocused(object sender, FocusEventArgs e) // TODO: This is a hack, need to find a better way to do this. maybes subscribe to an observable
		{
			Entry entry = sender as Entry;
			FolderInfo fi = entry.BindingContext as FolderInfo;
			if (!fi.VerifyPath())
			{
				entry.TextColor = Colors.Red;
			}
			else
			{
				entry.TextColor = AppInfo.Current.RequestedTheme switch
				{
					AppTheme.Dark => Colors.AntiqueWhite,
					AppTheme.Light => Colors.Black,
					_ => Colors.Black
				};
			}
		}
	}

}
