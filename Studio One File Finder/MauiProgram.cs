using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace Studio_One_File_Finder
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.UseMauiCommunityToolkit()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				});

#if DEBUG
			builder.Logging.AddDebug();
#endif
			/*
#if WINDOWS
			builder.Services.AddSingleton<IFolderPicker, Platforms.Windows.FolderPickerWindows>();
#elif MACCATALYST
			builder.Services.AddSingleton<IFolderPicker, Platforms.MacCatalyst.FolderPickerMac>();
#endif*/
			builder.Services.AddSingleton<MainPage>(); //AddTransient<MainPage>();
			builder.Services.AddSingleton<FilePreferencesViewModel>();
			return builder.Build();
		}
	}
}
