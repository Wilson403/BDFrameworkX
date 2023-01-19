﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BDFramework.Editor.Tools.EditorHttpServer
{
    /// <summary>
    /// 调用Editor接口
    /// 函数为Static
    /// EditorInvoke/{func_fullname}
    /// EditorInvoke/namepace.classname.funcktionname
    /// </summary>
    public class WP_EditorInvoke : IEditorWebApiProcessor
    {
        public string WebApiName { get; set; } = "EditorInvoke";

        /// <summary>
        /// editor执行缓存
        /// </summary>
        private Dictionary<string, MethodInfo> functionCacheMap = new Dictionary<string, MethodInfo>();


        /// <summary>
        /// webapi处理器
        /// </summary>
        /// <param name="apiParams"></param>
        /// <param name="ctx"></param>
        /// <exception cref="Exception"></exception>
        async public Task<EditorHttpResonseData> WebAPIProcessor(string apiParams, HttpListenerContext ctx)
        {
            var ret = functionCacheMap.TryGetValue(apiParams, out var methodInfo);
            if (!ret)
            {
                var lastDotIdx = apiParams.LastIndexOf(".");
                if (lastDotIdx == -1)
                {
                    throw new Exception("Function不存在");
                }

                var clasname = apiParams.Substring(0, lastDotIdx);
                var funcname = apiParams.Substring(lastDotIdx + 1);
                //获取type
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type type = null;
                foreach (var assembly in assemblies)
                {
                    type = assembly.GetTypes().FirstOrDefault((t) => t.FullName.Equals(clasname, StringComparison.OrdinalIgnoreCase));
                    if (type != null)
                    {
                        break;
                    }
                }

                //
                if (type != null)
                {
                    methodInfo = type.GetMethod(funcname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (methodInfo != null)
                    {
                        functionCacheMap[apiParams] = methodInfo;
                    }
                    else
                    {
                        throw new Exception("Function不存在");
                    }
                }
                else
                {
                    throw new Exception("ClassType不存在");
                }
            }

            try
            {
                await UniTask.SwitchToMainThread();
                methodInfo.Invoke(null, null);
            }
            catch (Exception e)
            {
                var err = e.ToString();

                Debug.LogError(e);
                if (e.InnerException != null)
                {
                    err += ("\n" + e.InnerException.ToString());
                    Debug.LogError(e.InnerException);
                }

                throw new Exception("Function 执行报错,请查看Unity! \n" + err);
            }

            ctx.Response.StatusCode = 200;
            
            var retdata = new EditorHttpResonseData();
            retdata.content = "执行成功";
            
            return retdata;
        }


        /// <summary>
        /// 测试接口
        /// </summary>
        static private void Test()
        {
            Debug.Log("【EditorHttp】EditorInvoke Success!");
        }
    }
}
