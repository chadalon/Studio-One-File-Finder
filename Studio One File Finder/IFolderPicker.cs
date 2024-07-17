using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Studio_One_File_Finder
{
	public interface IFolderPicker
	{
		Task<string> PickFolder();
	}
}
