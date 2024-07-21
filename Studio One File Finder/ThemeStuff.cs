using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Studio_One_File_Finder
{
	public class ThemeStuff
	{
		public static Color GetDefaultTextColor()
		{
			return AppInfo.Current.RequestedTheme switch
			{
				AppTheme.Dark => Colors.AntiqueWhite,
				AppTheme.Light => Colors.Black,
				_ => Colors.Black
			};	

		}
		public static Color GetErrorText()
		{
			return Colors.Red;
		}
	}
}
