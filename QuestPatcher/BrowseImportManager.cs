using Avalonia.Controls;
using QuestPatcher.Core.Modding;
using QuestPatcher.Core.Patching;
using QuestPatcher.Models;
using QuestPatcher.Views;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using QuestPatcher.Core;
using QuestPatcher.Services;
using Avalonia.Media;
using QuestPatcher.Core.Utils;
using QuestPatcher.Utils;
using Serilog;
using Version = SemanticVersioning.Version;

namespace QuestPatcher
{
    /// <summary>
    /// Handles creating browse dialogs for importing files, and also the importing of unknown files
    /// </summary>
    public class BrowseImportManager
    {
        private struct FileImportInfo
        {
            public string Path { get; set; }

            public FileCopyType? PreferredCopyType { get; set; }
        }

        private readonly OtherFilesManager _otherFilesManager;
        private readonly ModManager _modManager;
        private readonly Window _mainWindow;
        private readonly PatchingManager _patchingManager;
        private readonly OperationLocker _locker;
        private readonly SpecialFolders _specialFolders;
        private readonly FileDialogFilter _modsFilter = new();
        private readonly QuestPatcherUiService _uiService;
        private Queue<FileImportInfo>? _currentImportQueue;

        public BrowseImportManager(OtherFilesManager otherFilesManager, ModManager modManager, Window mainWindow, PatchingManager patchingManager, OperationLocker locker,SpecialFolders specialFolders, QuestPatcherUiService uiService)
        {
            _uiService=uiService;
            _otherFilesManager = otherFilesManager;
            _modManager = modManager;
            _specialFolders = specialFolders;
            _mainWindow = mainWindow;
            _patchingManager = patchingManager;
            _locker = locker;
            _modsFilter.Name = "Quest Mods";
            _modsFilter.Extensions.Add("qmod");
        }

        private static FileDialogFilter GetCosmeticFilter(FileCopyType copyType)
        {
            return new FileDialogFilter
            {
                Name = copyType.NamePlural,
                Extensions = copyType.SupportedExtensions
            };
        }
        public async Task<bool> AskToInstallApk(bool deleteMods = false)
        {
            var dialog = new OpenFileDialog
            {
                AllowMultiple = false
            };
            FileDialogFilter filter=new();
            filter.Extensions.Add("apk");
            filter.Name = "Beat Saber APKs";
            dialog.Filters.Add(filter);
            var files = await dialog.ShowAsync(_mainWindow);
            if (files == null || files.Length == 0) return false;
            var file = files[0] ?? "";
            if (!file.EndsWith(".apk"))
            {
                DialogBuilder builder1 = new()
                {
                    Title = "你选择的文件有误",
                    Text = "你选择的文件有误，将不会继续安装。",
                    HideCancelButton = true
                };
                await builder1.OpenDialogue(_mainWindow);
                return false;
            }

            {
                DialogBuilder builder1 = new()
                {
                    Title = "即将开始安装",
                    Text = "安装可能需要两分钟左右，该过程中将暂时无法点击软件窗口，请耐心等待，\n点击下方“好的”按钮，即可开始安装。",
                };
                if (!await builder1.OpenDialogue(_mainWindow)) return false;
            }
            _locker.StartOperation();

            try
            {
                if (deleteMods) await _modManager.DeleteAllMods();
                await _patchingManager.Uninstall();
                await _patchingManager.InstallApp(file);
            }
            finally
            {
                _locker.FinishOperation();
            }
            {
                DialogBuilder builder1 = new()
                {
                    Title = "安装已完成！",
                    Text = "点击确定以重启QuestPatcher",
                    HideCancelButton = true
                };
                await builder1.OpenDialogue(_mainWindow);
            }
            await _uiService.Reload();
            return true;
        }
        
        public async Task InstallApkFromUrl(string url)
        {
            {
                DialogBuilder builder1 = new()
                {
                    Title = "即将开始安装",
                    Text = "安装可能需要两分钟左右，该过程中将暂时无法点击软件窗口，请耐心等待，\n点击下方“好的”按钮，即可开始安装。",
                    HideCancelButton = true
                };
                builder1.OkButton.ReturnValue = false;
                await builder1.OpenDialogue(_mainWindow);
            }
            _locker.StartOperation();
            WebClient client = new();
            File.Delete(_specialFolders.TempFolder + "/apkToInstall.apk");
            await client.DownloadFileTaskAsync(url+"?_="+ DateTime.Now.ToFileTime(), _specialFolders.TempFolder+"/apkToInstall.apk");
            await _patchingManager.InstallApp( _specialFolders.TempFolder + "/apkToInstall.apk");
            _locker.FinishOperation();
            {
                DialogBuilder builder1 = new()
                {
                    Title = "安装已完成！",
                    Text = "点击确定以继续",
                    HideCancelButton = true
                };
                builder1.OkButton.ReturnValue = false;
                await builder1.OpenDialogue(_mainWindow);
            }
        }
        
        public async Task<bool> UninstallAndInstall()
        {
            DialogBuilder builder1 = new()
            {
                Title = "更换游戏版本",
                Text = "换版本会删除所有的Mod，但不会影响您的歌曲、模型资源。降级完成后您可以把对应版本的Mod重新装回去，即可继续使用这些资源。\n\n点击继续并选择目标版本的游戏APK即可完成更换版本\n如果您还没有游戏APK，可以进入网盘下载",
            };
            builder1.OkButton.Text = "继续";
            builder1.WithButtons(new ButtonInfo
            {
                Text = "进入网盘",
                CloseDialogue = false,
                OnClick = () => Util.OpenWebpage("https://bs.wgzeyu.com/drive/")
            });
            
            if (!await builder1.OpenDialogue(_mainWindow)) return false;
            
            try
            {
                return await AskToInstallApk(true);
            }
            catch (Exception e)
            {
                var builder2 = new DialogBuilder {
                    Title = "出错了！",
                    Text = "在更换版本的过程中出现了一个意料之外的错误。",
                    HideCancelButton = true
                };
                builder2.WithException(e);
                await builder2.OpenDialogue(_mainWindow);
            }

            return false;
        }
        private void AddAllCosmeticFilters(OpenFileDialog dialog)
        {
            foreach(FileCopyType copyType in _otherFilesManager.CurrentDestinations)
            {
                dialog.Filters.Add(GetCosmeticFilter(copyType));
            }
        }

        /// <summary>
        /// Opens a browse dialog that has filters for all files supported by QuestPatcher.
        /// This includes qmod and all other file copies.
        /// </summary>
        /// <returns>A task that completes when the dialog has closed and the files have been imported</returns>
        public async Task ShowAllItemsBrowse()
        {
            OpenFileDialog dialog = ConstructDialog();

            // Add a filter for any file type that QuestPatcher supports
            // This includes qmod and all cosmetic/file copy types.
            FileDialogFilter allFiles = new()
            {
                Name = "All Allowed Files"
            };

            List<string> allExtensions = allFiles.Extensions;
            allExtensions.Add("qmod");
            foreach(FileCopyType copyType in _otherFilesManager.CurrentDestinations)
            {
                allExtensions.AddRange(copyType.SupportedExtensions);
            }

            dialog.Filters.Add(allFiles);
            dialog.Filters.Add(_modsFilter);
            AddAllCosmeticFilters(dialog);

            await ShowDialogAndHandleResult(dialog);
        }

        /// <summary>
        /// Opens a browse dialog for installing mods only.
        /// </summary>
        /// <returns>A task that completes when the dialog has closed and the files have been imported</returns>
        public async Task ShowModsBrowse()
        {
            OpenFileDialog dialog = ConstructDialog();
            dialog.Filters.Add(_modsFilter);
            await ShowDialogAndHandleResult(dialog);
        }

        /// <summary>
        /// Opens a browse dialog for installing this particular type of file copy/cosmetic.
        /// </summary>
        /// <param name="cosmeticType"></param>
        /// <returns>A task that completes when the dialog has closed and the files have been imported</returns>
        public async Task ShowFileCopyBrowse(FileCopyType cosmeticType)
        {
            OpenFileDialog dialog = ConstructDialog();
            dialog.Filters.Add(GetCosmeticFilter(cosmeticType));
            await ShowDialogAndHandleResult(dialog, cosmeticType);
        }

        private static OpenFileDialog ConstructDialog()
        {
            return new OpenFileDialog()
            {
                AllowMultiple = true
            };
        }

        private async Task ShowDialogAndHandleResult(OpenFileDialog dialog, FileCopyType? knownFileCopyType = null)
        {
            string[] files = await dialog.ShowAsync(_mainWindow);
            if(files == null)
            {
                return;
            }

            await AttemptImportFiles(files, knownFileCopyType);
        }

        /// <summary>
        /// Imports multiple files, and finds what type they are first.
        /// Will prompt the user with any errors while importing the files.
        /// If a list of files is already importing, these files will be added to the queue
        /// </summary>
        /// <param name="files">The paths of the files to import</param>
        /// <param name="preferredCopyType">File copy type that will be used if there are multiple copy types for one of these files. If null or not valid for the item, a dialog will be displayed allowing the user to choose</param>
        public async Task AttemptImportFiles(ICollection<string> files, FileCopyType? preferredCopyType = null)
        {
            bool queueExisted = _currentImportQueue != null;
            if(_currentImportQueue == null)
            {
                _currentImportQueue = new Queue<FileImportInfo>();
            }

            // Append all files to the new or existing queue
            Log.Debug($"Enqueuing {files.Count} files");
            foreach (string file in files)
            {
                _currentImportQueue.Enqueue(new FileImportInfo
                {
                    Path = file,
                    PreferredCopyType = preferredCopyType
                });
            }

            // If a queue already existed, that will be processed with our enqueued files, so we can stop here
            if(queueExisted)
            {
                Log.Debug("Queue is already being processed");
                return;
            }

            // Otherwise, we process the current queue
            Log.Debug("Processing queue . . .");

            // Do nothing if attempting to import files when operations are ongoing that are not file imports
            // TODO: Ideally this would wait until the lock is free and then continue
            if(!_locker.IsFree)
            {
                Log.Error("Failed to process files: Operations are still ongoing");
                _currentImportQueue = null;
                return;
            }
            _locker.StartOperation();
            try
            {
                await ProcessImportQueue();
            }
            finally
            {
                _locker.FinishOperation();
                _currentImportQueue = null;
            }
        }

        /// <summary>
        /// Processes the current import queue until it reaches zero in size.
        /// Displays exceptions for any failed files
        /// </summary>
        private async Task ProcessImportQueue()
        {
            if(_currentImportQueue == null)
            {
                throw new InvalidOperationException("Cannot process import queue if there is no import queue assigned");
            }

            // Attempt to import each file, and catch the exceptions if any to display them below
            Dictionary<string, Exception> failedFiles = new();
            int totalProcessed = 0; // We cannot know how many files were enqueued in total, so we keep track of that here
            while(_currentImportQueue.TryDequeue(out FileImportInfo importInfo))
            {
                string path = importInfo.Path;
                totalProcessed++;
                try
                {
                    Log.Information($"Importing {path} . . .");
                    await ImportUnknownFile(path, importInfo.PreferredCopyType);
                }
                catch(Exception ex)
                {
                    failedFiles[path] = ex;
                }
            }
            _currentImportQueue = null; // New files added should go to a new queue

            Log.Information($"{totalProcessed - failedFiles.Count}/{totalProcessed} files imported successfully");

            if(failedFiles.Count == 0) { return; }

            bool multiple = failedFiles.Count > 1;

            DialogBuilder builder = new()
            {
                Title = "导入失败",
                HideCancelButton = true
            };

            if (multiple)
            {
                // Show the exceptions for multiple files in the logs to avoid a giagantic dialog
                builder.Text = "有多个文件安装失败，请检查日志确认详情。";
                foreach (KeyValuePair<string, Exception> pair in failedFiles)
                {
                    Log.Error($"{Path.GetFileName(pair.Key)}安装失败：{pair.Value.Message}");
                    Log.Debug($"Full error: {pair.Value}");
                }
            }
            else
            {
                // Display single files with more detail for the user
                string filePath = failedFiles.Keys.First();
                Exception exception = failedFiles.Values.First();

                // Don't display the full stack trace for InstallationExceptions, since these are thrown by QP and are not bugs/issues
                if (exception is InstallationException)
                {
                    builder.Text = $"{Path.GetFileName(filePath)}安装失败：{exception.Message}";
                }
                else
                {
                    builder.Text = $"文件{Path.GetFileName(filePath)}安装失败";
                    builder.WithException(exception);
                }
                Log.Error($"Failed to install {Path.GetFileName(filePath)}: {exception}");
            }

            await builder.OpenDialogue(_mainWindow);
        }

        /// <summary>
        /// Figures out what the given file is, and installs it accordingly.
        /// Throws an exception if the file cannot be installed by QuestPatcher.
        /// </summary>
        /// <param name="path">The path of file to import</param>
        /// <param name="preferredCopyType">File copy type that will be used if there are multiple copy types for this file. If null, a dialog will be displayed allowing the user to choose</param>
        private async Task ImportUnknownFile(string path, FileCopyType? preferredCopyType)
        {
            string extension = Path.GetExtension(path).ToLower();

            // Attempt to install as a mod first
            if(await TryImportMod(path))
            {
                return;
            }

            // Attempt to copy the file to the quest as a map, hat or similar
            List<FileCopyType> copyTypes;
            if(preferredCopyType == null || !preferredCopyType.SupportedExtensions.Contains(extension[1..]))
            {
                copyTypes = _otherFilesManager.GetFileCopyTypes(extension);
            }
            else
            {
                // If we already know the file copy type
                // e.g. from dragging into a particular part of the UI, or for browsing for a particular file type,
                // we don't need to prompt on which file copy type to use
                copyTypes = new() { preferredCopyType };
            }

            if(copyTypes.Count > 0)
            {
                FileCopyType copyType;
                if(copyTypes.Count > 1)
                {
                    // If there are multiple different file copy types for this file, prompt the user to decide what they want to import it as
                    FileCopyType? chosen = await OpenSelectCopyTypeDialog(copyTypes, path);
                    if(chosen == null)
                    {
                        Log.Information($"Cancelling file {Path.GetFileName(path)}");
                        return;
                    }
                    else
                    {
                        copyType = chosen;
                    }
                }
                else
                {
                    // Otherwise, just use the only type available
                    copyType = copyTypes[0];
                }

                await copyType.PerformCopy(path);
                return;
            }

            // if there are no core mods and the user cancels the import,
            // TryImportMod will return false even though the file is a qmod
            if (extension != ".qmod")
            {
                throw new InstallationException($"未知文件类型 {extension}");
            }

            throw new InstallationException("qmod文件可能损坏或者核心mod缺失");
        }

        /// <summary>
        /// Opens a dialog to allow the user to choose between multiple different file copy destinations to import a file as.
        /// </summary>
        /// <param name="copyTypes">The available file copy types for this file</param>
        /// <param name="path">The path of the file</param>
        /// <returns>The selected FileCopyType, or null if the user pressed cancel/closed the dialog</returns>
        private async Task<FileCopyType?> OpenSelectCopyTypeDialog(List<FileCopyType> copyTypes, string path)
        {
            FileCopyType? selectedType = null;

            DialogBuilder builder = new()
            {
                Title = "多种导入选项",
                Text = $"{Path.GetFileName(path)}可以作为多种不同类型的文件导入，请选择你想要安装的内容。",
                HideOkButton = true
            };

            List<ButtonInfo> dialogButtons = new();
            foreach(FileCopyType copyType in copyTypes)
            {
                dialogButtons.Add(new ButtonInfo
                {
                    ReturnValue = true,
                    CloseDialogue = true,
                    OnClick = () =>
                    {
                        selectedType = copyType;
                    },
                    Text = copyType.NameSingular
                });
            }
            builder.WithButtons(dialogButtons);

            await builder.OpenDialogue(_mainWindow);
            return selectedType;
        }
        private async Task<bool> InstallMissingCoreMods(List<JToken> mods) {
            WebClient client = new WebClient();
            foreach(var mod in mods)
            {
                var modUrl = mod["downloadLink"]?.ToString();

                if (modUrl != null)
                {
                    if (_uiService.Config.UseMirrorDownload) modUrl = await DownloadMirrorUtil.Instance.GetMirrorUrl(modUrl);
                    await client.DownloadFileTaskAsync(modUrl, _specialFolders.TempFolder + "/coremod_tmp.qmod");
                    await TryImportMod(_specialFolders.TempFolder + "/coremod_tmp.qmod", true,true);
                }
                else
                {
                    Log.Fatal("Core Mod {Id} has null download link!", mod["id"]?.ToString()?? "null");
                }
            }
            client.Dispose();
            await _modManager.SaveMods();
            return true;
        }
        
        /**
         * Return true if all core mods are installed or the user want's to ignore missing core mods
         */
        public async Task<bool> CheckCoreMods(bool manualCheck = false, bool lockTheLocker = false, bool refreshCoreMods = false)
        {
            if (lockTheLocker) _locker.StartOperation();
            if (refreshCoreMods) await CoreModUtils.Instance.RefreshCoreMods();
            
            var coreMods = CoreModUtils.Instance.GetCoreMods(_patchingManager.InstalledApp?.Version ?? "");
            if (coreMods.Count > 0)
            {
                var missingCoreMods = new List<JToken>();
                foreach(var coreMod in coreMods)
                {
                    var existingCoreMod = _modManager.AllMods.Find((mod => mod.Id == coreMod["id"]?.ToString()));
                    if (existingCoreMod == null)
                    {
                        // not installed at all, or not for the right version of the game
                        missingCoreMods.Add(coreMod);
                    }
                    else if (Version.TryParse(coreMod["version"]?.ToString(), true, out var version) && version > existingCoreMod.Version)
                    {
                        // this coreMod JToken is newer than the installed one
                        // don't allow core mod downgrade when checking against core mod json
                        
                        await existingCoreMod.Uninstall(); // delete the current one
                        await _modManager.DeleteMod(existingCoreMod);
                        await _modManager.SaveMods();
                        
                        missingCoreMods.Add(coreMod); // install the new one
                    }
                    else
                    {
                        // the existing one is the "latest", enable it if not already
                        
                        // we can't reliably check existingCoreMod's target game version
                        // existingCoreMod.PackageVersion can be null which we will assume it will work
                        
                        // existingCoreMod.PackageVersion can be not matching the game installed while still
                        // list as the core mod for the installed game version
                        
                        // game downgrade or upgrade from qp will delete all mods
                        
                        if (!existingCoreMod.IsInstalled)
                        {
                            await existingCoreMod.Install();
                        }
                    }
                }
                
                if (missingCoreMods.Count != 0)
                {
                    Log.Warning("Core Mods Missing: {Mods}", missingCoreMods.Aggregate("", (s, token) => $"{s}\n{token["id"]}-{token["version"]}"));
                    DialogBuilder builder = new()
                    {
                        Title = "缺失核心Mod",
                        Text = "你缺少了必须要安装的一些核心Mod，这会导致许多第三方Mod无法运行，因为他们均依赖核心Mod。\n" +
                        "而自定义歌曲等基础功能，也是由核心Mod来实现的。\n" +
                        "是否补全核心Mod？"
                    };
                    builder.OkButton.Text = "帮我补全";
                    if (await builder.OpenDialogue(_mainWindow))
                    {
                        await InstallMissingCoreMods(missingCoreMods);
                        goto CoreModsOK;
                    }
                    goto CoreModsNotOK;
                } 
                if (manualCheck)
                {
                    DialogBuilder builder = new()
                    {
                        Title = "核心Mod安装正确！",
                        Text = "恭喜你，你已经装好了核心Mod！",
                        HideCancelButton = true
                    };
                    await builder.OpenDialogue(_mainWindow);
                }
                goto CoreModsOK;
            }
            else
            {
                DialogBuilder builder = new()
                {
                    Title = "未找到该版本的核心Mod！",
                    Text = $"你当前安装的游戏版本为{_patchingManager.InstalledApp.Version}，但核心Mod还没有更新，还没有适配该版本，所以无法安装核心Mod。\n" +
                    $"你可以先降级游戏再重新打补丁装Mod。\n如需降级请查看新手教程左下角",
                    HideCancelButton = manualCheck
                };
                builder.OkButton.Text = manualCheck ? "我知道了！" : "仍然安装";
                builder.WithButtons(
                    new ButtonInfo
                    {
                        Text = "进入新手教程",
                        ReturnValue = true,
                        OnClick = () => Util.OpenWebpage("https://bs.wgzeyu.com/oq-guide-qp/")
                    });
                
                if (await builder.OpenDialogue(_mainWindow))
                {
                    goto CoreModsOK;
                }
                else
                {
                    goto CoreModsNotOK;
                }
            }
            
            CoreModsOK:
            if (lockTheLocker) _locker.FinishOperation();
            return true;
            
            CoreModsNotOK:
            if (lockTheLocker) _locker.FinishOperation();
            return false;
            
        }
        /// <summary>
        /// Imports then installs a mod.
        /// Will prompt to ask the user if they want to install the mod in the case that it is outdated
        /// </summary>
        /// <param name="path">The path of the mod</param>
        /// <returns>Whether or not the file could be imported as a mod</returns>
        private async Task<bool> TryImportMod(string path, bool avoidCoremodCheck = false,bool ignoreWrongVersion=false)
        {
            if (!avoidCoremodCheck)
                if (!await CheckCoreMods())
                    return false;

            // Import the mod file and copy it to the quest
            IMod? mod = await _modManager.TryParseMod(path);
            if(mod is null)
            {
                return false;
            }

            Debug.Assert(_patchingManager.InstalledApp != null);

            // Prompt the user for outdated mods instead of enabling them automatically
            if(mod.PackageVersion != null && mod.PackageVersion != _patchingManager.InstalledApp.Version &&!ignoreWrongVersion)
            {
                DialogBuilder builder = new()
                {
                    Title = "版本不匹配的Mod",
                    Text = $"该Mod是为{mod.PackageVersion}版本的游戏开发的，然而你当前安装的游戏版本是{_patchingManager.InstalledApp.Version}。启用这个Mod可能会导致游戏崩溃，也可能不管用。"
                };
                builder.OkButton.Text = "立即启用";
                builder.CancelButton.Text = "取消";

                if(!await builder.OpenDialogue(_mainWindow))
                {
                    return true;
                }
            }

            // Automatically install the mod once it has been imported
            // TODO: Is this desirable? Would it make sense to require it to be enabled manually
            await mod.Install();
            await _modManager.SaveMods();
            return true;
        }
    }
}
