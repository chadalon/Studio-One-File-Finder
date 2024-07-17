namespace Studio_One_File_Finder
{
	public partial class App : Application
	{
		public App(IServiceProvider serviceProvider)
		{
			InitializeComponent();

			MainPage = serviceProvider.GetRequiredService<MainPage>(); //new AppShell();
		}
	}
}
