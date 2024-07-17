using System.Threading.Tasks;

namespace Studio_One_File_Finder
{
	public partial class MainPage : ContentPage
	{
		int count = 0;
		private IFolderPicker _folderPicker;
		public MainPage(IFolderPicker folderPicker)
		{
			InitializeComponent();
			_folderPicker = folderPicker;
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
		private void OnBrowseClicked(object sender, EventArgs e)
		{
			//await Navigation.PushAsync(new BrowsePage());
		}

		private async void OnAddSampleDirClicked(object sender, EventArgs e)
		{
			await PickFolder(new CancellationToken());
		}
		async Task PickFolder(CancellationToken cancellationToken)
		{
			var result = await _folderPicker.PickFolder();
			/*
			if (result.IsSuccessful)
			{
				await Toast.Make($"The folder was picked: Name - {result.Folder.Name}, Path - {result.Folder.Path}", ToastDuration.Long).Show(cancellationToken);
			}
			else
			{
				await Toast.Make($"The folder was not picked with error: {result.Exception.Message}").Show(cancellationToken);
			}*/
		}
	}

}
