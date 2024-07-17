using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Studio_One_File_Finder.Platforms.Windows
{
	internal class FolderPickerWindows : IFolderPicker
	{
		public async Task<string> PickFolder()
		{
			var folderPicker = new FolderPicker();
			IntPtr hwnd = WindowNative.GetWindowHandle(App.Current.Windows[0].Handler.PlatformView);
			InitializeWithWindow.Initialize(folderPicker, hwnd);
			folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
			folderPicker.FileTypeFilter.Add("*");

			StorageFolder folder = await folderPicker.PickSingleFolderAsync();
			return folder?.Path;

		}
	}
}
