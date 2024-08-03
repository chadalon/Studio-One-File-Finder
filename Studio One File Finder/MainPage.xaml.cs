using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Maui.Core.Primitives;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using ReactiveUI;
using System.Reactive;

#if WINDOWS
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
#endif

#if IOS || MACCATALYST
using UIKit;
using Foundation;
#endif

namespace Studio_One_File_Finder
{
	public partial class MainPage : ContentPage
	{
		public FilePreferencesViewModel FilePreferences;
		public string Hello = "poo";
		public MainPage(FilePreferencesViewModel filePreferencesViewModel)
		{
			FilePreferences = filePreferencesViewModel;
			BindingContext = filePreferencesViewModel;
			filePreferencesViewModel.Alert += DisplayAlert;
			filePreferencesViewModel.PromptAlert += DisplayAlert;
			InitializeComponent();

			var thing = FilePreferences.WhenAnyValue(x => x.OutputText).Subscribe(_ =>
			{
				Application.Current.Dispatcher.Dispatch(() => OutputScroller.ScrollToAsync(OutputScrollerLabel, ScrollToPosition.End, false));
			});

			var musicObservable = FilePreferences.WhenAnyValue(x => x.IsMusicPlaying).Subscribe(x =>
			{
				if (x)
				{
					musicPlayer.Play();
				}
				else
				{
					musicPlayer.Pause();
				}
			});

			Loaded += OnPageLoad;
		}

		private void OnPageLoad(object? sender, EventArgs e)
		{
			// TODO uncomment this when ready
			//musicPlayer.Play();
		}

		private void OnMusicCheckBoxClicked(object? sender, EventArgs e)
		{
			musicCheckBox.IsChecked = !musicCheckBox.IsChecked;
		}
		private void OnCounterClicked(object sender, EventArgs e)
		{

			/*
			if (count == 1)
				CounterBtn.Text = $"Clicked {count} time";
			else
				CounterBtn.Text = $"Clicked {count} times";

			SemanticScreenReader.Announce(CounterBtn.Text);*/
		}
		private async void OnSubmitClicked(object sender, EventArgs e)
		{
			await Task.Run(FilePreferences.SubmitEverything);
		}
		private async void OnRestoreClicked(object sender, EventArgs e)
		{
			await Task.Run(FilePreferences.RestoreFiles);
		}
		private async void OnDeleteBackupsClicked(object sender, EventArgs e)
		{
			await Task.Run(FilePreferences.DeleteBackups);
		}
		private async void OnBrowseClicked(object sender, EventArgs e)
		{
			var btn = sender as Button;
			FolderInfo fi = btn.BindingContext as FolderInfo;
			//await Navigation.PushAsync(new BrowsePage());
			var pickedFolder = await PickFolder(new CancellationToken());
			if (pickedFolder != null)
			{
				fi.FolderPath = pickedFolder.Path;
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
		async Task<Folder?> PickFolder(CancellationToken cancellationToken)
		{
			var result = await FolderPicker.Default.PickAsync(cancellationToken);
			//var result = await _folderPicker.PickFolder();
			if (result != null)
			{
				// TODO check if it's a valid path
			}
			return result?.Folder;
		}

		void OnDragOver(object sender, DragEventArgs e)
		{
			// TODO
			// if its a folder
			// indicate it's good
		}
		async void OnDrop(object sender, DropEventArgs e) // BIG TODO!!!!! MACCATALYST SHIT!!!
		{
			var filePaths = new List<string>();

#if WINDOWS
			if (e.PlatformArgs is not null && e.PlatformArgs.DragEventArgs.DataView.Contains(StandardDataFormats.StorageItems))
			{
				var items = await e.PlatformArgs.DragEventArgs.DataView.GetStorageItemsAsync();
				if (items.Any())
				{
					foreach (var item in items)
					{
						if (item is StorageFolder file)
						{
							filePaths.Add(item.Path);
						}
					}

				}
			}
#elif MACCATALYST
			var session = e.PlatformArgs?.DropSession;
			if (session == null)
			{
				return;
			}
			foreach (UIDragItem item in session.Items)
			{
				var result = await LoadItemAsync(item.ItemProvider, item.ItemProvider.RegisteredTypeIdentifiers.ToList());
				if (result is not null)
				{
					filePaths.Add(result.FileUrl?.Path!);
				}
			}
			foreach (var item in filePaths)
			{
				Debug.WriteLine($"Path: {item}");
			}

			static async Task<LoadInPlaceResult?> LoadItemAsync(NSItemProvider itemProvider, List<string> typeIdentifiers)
			{
				if (typeIdentifiers is null || typeIdentifiers.Count == 0)
				{
					return null;
				}

				var typeIdent = typeIdentifiers.First();

				if (itemProvider.HasItemConformingTo(typeIdent))
				{
					return await itemProvider.LoadInPlaceFileRepresentationAsync(typeIdent);
				}

				typeIdentifiers.Remove(typeIdent);

				return await LoadItemAsync(itemProvider, typeIdentifiers);
			}
#else
			await Task.CompletedTask;
#endif
			/*
#if WINDOWS
			if (e.PlatformArgs is not null && e.PlatformArgs.DragEventArgs.DataView.Contains(StandardDataFormats.StorageItems))
			{
				var items = await e.PlatformArgs.DragEventArgs.DataView.GetStorageItemsAsync();
				if (items.Any())
				{
					foreach (var item in items)
					{
						if (item is StorageFile file)
						{
							filePaths.Add(item.Path);
						}
					}

				}
			}
#elif MACCATALYST
			var session = e.PlatformArgs?.DropSession;
			if (session == null)
			{
				return;
			}
			foreach (UIDragItem item in session.Items)
			{
				var result = await LoadItemAsync(item.ItemProvider, item.ItemProvider.RegisteredTypeIdentifiers.ToList());
				if (result is not null)
				{
					filePaths.Add(result.FileUrl?.Path!);
				}
			}
			foreach (var item in filePaths)
			{
				Debug.WriteLine($"Path: {item}");
			}

			static async Task<LoadInPlaceResult?> LoadItemAsync(NSItemProvider itemProvider, List<string> typeIdentifiers)
			{
				if (typeIdentifiers is null || typeIdentifiers.Count == 0)
				{
					return null;
				}

				var typeIdent = typeIdentifiers.First();

				if (itemProvider.HasItemConformingTo(typeIdent))
				{
					return await itemProvider.LoadInPlaceFileRepresentationAsync(typeIdent);
				}

				typeIdentifiers.Remove(typeIdent);

				return await LoadItemAsync(itemProvider, typeIdentifiers);
			}
#else
			await Task.CompletedTask;
#endif*/

			var dgr = sender as DropGestureRecognizer;
			if (dgr is not null && filePaths.Count > 0)
			{
				FolderInfo fi = dgr.BindingContext as FolderInfo;
				fi.FolderPath = filePaths.First();
			}
		}
	}

}
