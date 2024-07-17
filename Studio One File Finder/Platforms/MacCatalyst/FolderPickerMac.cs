using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppKit;

namespace Studio_One_File_Finder.Platforms.MacCatalyst
{
	internal class FolderPickerMac : IFolderPicker
	{
		public Task<string> PickFolder()
		{
			var openPanel = new NSOpenPanel
			{
				CanChooseFiles = false,
				CanChooseDirectories = true,
				AllowsMultipleSelection = false
			};

			var result = openPanel.RunModal();
			if (result == 1)
			{
				return openPanel.Urls[0].Path;
			}

			return null;
		}
	}
}
