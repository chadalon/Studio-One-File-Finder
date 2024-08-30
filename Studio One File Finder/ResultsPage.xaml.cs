
using CommunityToolkit.Maui.Views;

namespace Studio_One_File_Finder;

public partial class ResultsPage : ContentPage
{
	public ResultsPage(List<string> songsUpdated, List<string> songsSkipped)
	{
		InitializeComponent();
		/*
		// Example UI for the modal page
		var entry = new Entry { Placeholder = "Enter something..." };
		var saveButton = new Button { Text = "Okay bruv" };
		saveButton.Clicked += OnSaveButtonClicked;

		var updatedDropdown = new Expander { Header = new Label { Text = $"{songsUpdated.Count} Songs Updated (click to expand)" }, Content = new ListView { ItemsSource = songsUpdated } };
		var skippedDropdown = new Expander { Header = new Label { Text = $"{songsSkipped.Count} Songs Updated (click to expand)" }, Content = new ListView { ItemsSource = songsSkipped } };

		Content = new ScrollView { Content = new StackLayout
		{
			Padding = new Thickness(20),
			Children = { entry, updatedDropdown, skippedDropdown, saveButton }
		}
		};*/

		BackgroundColor = Colors.Transparent;
		// Create a semi-transparent overlay
		var overlay = new BoxView
		{
			Color = Color.FromRgba(0, 0, 0, 0.5), // semi-transparent black
			VerticalOptions = LayoutOptions.FillAndExpand,
			HorizontalOptions = LayoutOptions.FillAndExpand
		};
		var gridInsideBox = new Grid()
		{
			RowDefinitions = new RowDefinitionCollection()
			{
				new RowDefinition(GridLength.Auto),
				new RowDefinition(GridLength.Star),
				new RowDefinition(GridLength.Auto)
			},
		};
		gridInsideBox.Add(new Label { Text = "Enter your details:", HorizontalOptions = LayoutOptions.Center }, row: 0);
		gridInsideBox.Add(new ScrollView
		{
			Content = new StackLayout
			{
				Padding = new Thickness(20),
				Children =
				{
					new Expander { Header = new Label { Text = $"{songsUpdated.Count} Songs Updated (click to expand)" }, Content = new ListView { ItemsSource = songsUpdated } },
					new Expander { Header = new Label { Text = $"{songsSkipped.Count} Songs Skipped (click to expand)" }, Content = new ListView { ItemsSource = songsSkipped } }
				}
			}
		}, row: 1);
		gridInsideBox.Add(new Button
		{
			Text = "Submit",
			Command = new Command(async () => await CloseModal())
		}, row:2);
		// Create the modal content area
		var modalContent = new Frame
		{
			//BackgroundColor = Colors.White,
			CornerRadius = 10,
			Padding = 20,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			WidthRequest = 500,
			HeightRequest = 350,
			Content = gridInsideBox
		};

		// Create the overall layout
		var grid = new Grid();
		grid.Children.Add(overlay);
		grid.Children.Add(modalContent);
		Content = grid;
	}
	private async Task CloseModal()
	{
		await Navigation.PopModalAsync();
	}

	private async void OnSaveButtonClicked(object sender, EventArgs e)
	{
		// Handle save logic here
		await Navigation.PopModalAsync(); // Close the modal after saving
	}
}