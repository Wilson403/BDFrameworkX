﻿using System;
using System.Collections;
using System.IO;
using BDFramework.ResourceMgr;
using BDFramework.Sql;
using BDFramework.Core.Tools;
using Cysharp.Text;
using DotNetExtension;
using LitJson;
using UnityEngine;

namespace BDFramework.Asset
{
    /// <summary>
    /// 母包资源构建信息
    /// </summary>
    public class ClientPackageBuildInfo
    {
        /// <summary>
        /// 构建时间
        /// </summary>
        public long BuildTime = 0;

        /// <summary>
        /// 版本号
        /// </summary>
        public string Version = "0.0.0";

        /// <summary>
        /// 母包svc版本号
        /// </summary>
        /// <returns></returns>
        public string BasePacakgeSVCVersion = "";

        /// <summary>
        /// AB构建svc版本号
        /// </summary>
        public string AssetBundleSVCVersion = "";

        /// <summary>
        /// 脚本构建svc版本号
        /// </summary>
        public string ScriptSVCVersion = "";

        /// <summary>
        /// 表格构建svc版本号
        /// </summary>
        public string TableSVCVersion = "";
    }

    /// <summary>
    /// 全局资源管理
    /// 用于管理
    /// 用以统一管理Sql、dll、和ArtConfig资源
    /// </summary>
    static public class ClientAssetsHelper
    {
        static ClientAssetsHelper()
        {
            BetterStreamingAssets.Initialize();
        }

        /// <summary>
        /// 只能在persistent目录读取的问题
        /// </summary>
        static string[] PersistentOnlyFiles = new string[]
        {
            ScriptLoder.DLL_PATH, //DLL,
            ScriptLoder.PDB_PATH, //PBD文件
            SqliteLoder.LOCAL_DB_PATH, //db
            BResources.ART_ASSET_INFO_PATH, BResources.ART_ASSET_TYPES_PATH, //ArtConfig,这两个配置文件是保证 更新资源后逻辑统一.
        };


        /// <summary>
        /// 获取母包资源构建信息
        /// </summary>
        /// <returns></returns>
        static public ClientPackageBuildInfo GetPackageBuildInfo(string ouptputPath, RuntimePlatform platform)
        {
            var path = IPath.Combine(ouptputPath, BApplication.GetPlatformPath(platform),
                BResources.PACKAGE_BUILD_INFO_PATH);
            var buildinfo = new ClientPackageBuildInfo();
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                buildinfo = JsonMapper.ToObject<ClientPackageBuildInfo>(text);
            }

            return buildinfo;
        }


        /// <summary>
        /// 生成母包资源构建信息
        /// </summary>
        static public void GenBasePackageBuildInfo(string outputPath, RuntimePlatform platform,
            string version = "", string basePacakgeSVC = "", string artSVC = "", string scriptSVC = "",
            string tableSVC = "")
        {
            //获取旧BuildAssetInfo
            var info = GetPackageBuildInfo(outputPath, platform);

            //写入buildinfo内容
            info.BuildTime = DateTimeEx.GetTotalSeconds();

            //资源版本

            if (!string.IsNullOrEmpty(version))
            {
                info.Version = version;
            }


            //母包版本信息
            if (!string.IsNullOrEmpty(basePacakgeSVC))
            {
                info.BasePacakgeSVCVersion = basePacakgeSVC;
            }

            //美术资产信息
            if (!string.IsNullOrEmpty(artSVC))
            {
                info.AssetBundleSVCVersion = artSVC;
            }

            //热更脚本资产
            if (!string.IsNullOrEmpty(scriptSVC))
            {
                info.ScriptSVCVersion = scriptSVC;
            }

            //表格数据
            if (!string.IsNullOrEmpty(tableSVC))
            {
                info.TableSVCVersion = tableSVC;
            }

            SaveBasePackageBuildInfo(outputPath, platform, info);
        }

        /// <summary>
        /// 保存母包资源info
        /// </summary>
        /// <param name="ouptputPath"></param>
        /// <param name="platform"></param>
        /// <param name="info"></param>
        static public void SaveBasePackageBuildInfo(string ouptputPath, RuntimePlatform platform, ClientPackageBuildInfo info)
        {
            //转json
            var content = JsonMapper.ToJson(info);
            //写入本地
            var path = IPath.Combine(ouptputPath, BApplication.GetPlatformPath(platform),
                BResources.PACKAGE_BUILD_INFO_PATH);
            FileHelper.WriteAllText(path, content);
        }


        /// <summary>
        /// 母包资源检测逻辑
        /// </summary>
        /// <returns></returns>
        static public void CheckBasePackageVersion(RuntimePlatform platform, Action callback)
        {
            bool isUseBetterStreaming = false;
            //persistent路径
            var persistentPlatformPath = IPath.Combine(Application.persistentDataPath, BApplication.GetPlatformPath(platform));
            //母包路径
            string basePckPath = "";

            //母包路径不同情况不一样
            switch (BDLauncher.Inst.Config.ArtRoot)
            {
                case AssetLoadPathType.Editor:
                {
                    //editor不进行母包资源管理
                    BDebug.Log("【资源包】Editor加载不执行:母包资源检测逻辑！");
                    callback?.Invoke();
                    return;
                }
                case AssetLoadPathType.Persistent:
                case AssetLoadPathType.StreamingAsset:
                {
                    isUseBetterStreaming = true;
                    basePckPath = BApplication.streamingAssetsPath;
                }
                    break;
                case AssetLoadPathType.DevOpsPublish:
                {
                    if (Application.isEditor)
                    {
                        isUseBetterStreaming = false;
                        basePckPath = BApplication.DevOpsPublishAssetsPath;
                    }
                    else
                    {
                        isUseBetterStreaming = true;
                        basePckPath = BApplication.streamingAssetsPath;
                    }
                }
                    break;
            }

            BDebug.Log("【资源包】执行母包资源检测逻辑！");
            //源地址
            string basePckPlatformPath = "";
            if (isUseBetterStreaming)
            {
                basePckPlatformPath = BApplication.GetPlatformPath(platform);
            }
            else
            {
                basePckPlatformPath = IPath.Combine(basePckPath, BApplication.GetPlatformPath(platform));
            }

            //packageinfo
            var persistentPckBuildInfoPath = IPath.Combine(persistentPlatformPath, BResources.PACKAGE_BUILD_INFO_PATH);
            //母包的build.info信息
            var basePckBuildInfoPath = IPath.Combine(basePckPlatformPath, BResources.PACKAGE_BUILD_INFO_PATH);

            if (!IsExsitAsset(basePckBuildInfoPath, isUseBetterStreaming))
            {
                //不存在Streaming配置
                BDebug.LogError("【母包资源检测】拷贝失败,不存在：" + basePckBuildInfoPath);
                callback?.Invoke();
                return;
            }
            else
            {
                BDebug.Log("【母包资源检测】读取母包配置：" + basePckBuildInfoPath);
                var basePckBuildInfoContent = ReadAssetAllText(basePckBuildInfoPath, isUseBetterStreaming);
                //persitent存在，判断版本
                if (IsExsitAsset(persistentPckBuildInfoPath))
                {
                    BDebug.Log("【母包资源检测】读取persistent配置：" + persistentPckBuildInfoPath);
                    var content = ReadAssetAllText(persistentPckBuildInfoPath);
                    //解析
                    var persistentPackageInfo = JsonMapper.ToObject<ClientPackageBuildInfo>(content);
                    var basePackageInfo = JsonMapper.ToObject<ClientPackageBuildInfo>(basePckBuildInfoContent);
                    if (persistentPackageInfo.BuildTime >= basePackageInfo.BuildTime)
                    {
                        //跳出，检测结束
                        BDebug.Log("【母包资源检测】不复制，母包无新资源");
                        BDLauncher.Inst.ClientBuildInfo = persistentPackageInfo;
                        callback?.Invoke();
                        return;
                    }
                    else
                    {
                        BDebug.Log("【母包资源检测】母包有新资源,即将覆盖persistent旧资源!!!!", Color.yellow);
                        BDLauncher.Inst.ClientBuildInfo  = basePackageInfo;
                        ClearOldPersistentAssets();
                     
                        //Streaming版本比较新
                        //复制Stream的packageinfo 到persistent
                        FileHelper.WriteAllText(persistentPckBuildInfoPath, basePckBuildInfoContent);
                    }
                }
                else
                {
                    BDebug.Log("【母包资源检测】第一次创建资源包info到persistent目录");
                    var basePackageInfo = JsonMapper.ToObject<ClientPackageBuildInfo>(basePckBuildInfoContent);
                    BDLauncher.Inst.ClientBuildInfo  = basePackageInfo;
                    //persistent版本不存在
                    //复制Stream的packageinfo 到persistent
                    FileHelper.WriteAllText(persistentPckBuildInfoPath, basePckBuildInfoContent);
                }
            }

            //开始拷贝逻辑
            for (int i = 0; i < PersistentOnlyFiles.Length; i++)
            {
                var copytoFile = PersistentOnlyFiles[i];
                //复制新版本的资产
                var persistentPath = IPath.Combine(persistentPlatformPath, copytoFile);
                var basePckAssetPath = IPath.Combine(basePckPlatformPath, copytoFile);

                //开始拷贝
                if (IsExsitAsset(basePckAssetPath, isUseBetterStreaming))
                {
                    BDebug.Log("【母包资源检测】复制成功:" + copytoFile);
                    var bytes = ReadFileAllBytes(basePckAssetPath, isUseBetterStreaming);
                    FileHelper.WriteAllBytes(persistentPath, bytes);
                }
                else
                {
                    BDebug.LogError("【母包资源检测】复制失败,本地不存在:" + copytoFile);
                }
            }

            //结束
            callback?.Invoke();
        }


        /// <summary>
        /// 是否存在文件
        /// </summary>
        /// <returns></returns>
        static private bool IsExsitAsset(string filePath, bool isUseBetterStreaming = false)
        {
            if (isUseBetterStreaming)
            {
                return BetterStreamingAssets.FileExists(filePath);
            }
            else
            {
                return File.Exists(filePath);
            }
        }

        /// <summary>
        /// 读取文件内容
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="isUseSysIO"></param>
        /// <returns></returns>
        static private string ReadAssetAllText(string filePath, bool isUseBetterStreaming = false)
        {
            if (isUseBetterStreaming)
            {
                return BetterStreamingAssets.ReadAllText(filePath);
            }
            else
            {
                return File.ReadAllText(filePath);
            }
        }

        /// <summary>
        /// 读取文件内容
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="isUseBetterStreaming"></param>
        /// <returns></returns>
        static private byte[] ReadFileAllBytes(string filePath, bool isUseBetterStreaming = false)
        {
            if (isUseBetterStreaming)
            {
                return BetterStreamingAssets.ReadAllBytes(filePath);
            }
            else
            {
                return File.ReadAllBytes(filePath);
            }
        }

        /// <summary>
        /// 清理旧的persistent资源
        /// </summary>
        static private void ClearOldPersistentAssets()
        {
            var runtimes = BApplication.SupportPlatform;
            foreach (var runtime in runtimes)
            {
                var path = IPath.Combine(Application.persistentDataPath, BApplication.GetPlatformPath(runtime));
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
        }
    }
}
