using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using Mcl.Core.Extensions;
using Mcl.Core.Network.Interface;
using Memory;
using Newtonsoft.Json.Linq;

namespace Mcl.Core.Network
{
    public class NetClient : INetClient
    {
        private static readonly Version version = new AssemblyName(Assembly.GetExecutingAssembly().FullName).Version;

        public IHttpFactory HttpFactory = new SimpleFactory<Http>();

        private readonly Regex structuredSyntaxSuffixRegex = new Regex("\\+\\w+$", RegexOptions.Compiled);

        private readonly Regex structuredSyntaxSuffixWildcardRegex = new Regex("^\\*\\+\\w+$", RegexOptions.Compiled);

        public int? MaxRedirects { get; set; }

        public X509CertificateCollection ClientCertificates { get; set; }

        public RequestCachePolicy CachePolicy { get; set; }

        public bool FollowRedirects { get; set; }

        public CookieContainer CookieContainer { get; set; }

        public string UserAgent { get; set; }

        public int Timeout { get; set; }

        public int ReadWriteTimeout { get; set; }

        public bool UseSynchronizationContext { get; set; }

        public virtual Uri BaseUrl { get; set; }

        public Encoding Encoding { get; set; }

        public bool PreAuthenticate { get; set; }

        private IList<string> AcceptTypes { get; set; }

        public IList<Parameter> DefaultParameters { get; private set; }

        public virtual NetRequestAsyncHandle ExecuteAsync(INetRequest request, Action<INetResponse, NetRequestAsyncHandle> callback)
        {
            string name = Enum.GetName(typeof(Method), request.Method);
            Method method = request.Method;
            if ((uint)(method - 1) <= 1u || method == Method.PATCH)
            {
                return ExecuteAsync(request, callback, name, DoAsPostAsync);
            }
            return ExecuteAsync(request, callback, name, DoAsGetAsync);
        }

        public virtual NetRequestAsyncHandle ExecuteAsyncGet(INetRequest request, Action<INetResponse, NetRequestAsyncHandle> callback, string httpMethod)
        {
            return ExecuteAsync(request, callback, httpMethod, DoAsGetAsync);
        }

        public virtual NetRequestAsyncHandle ExecuteAsyncPost(INetRequest request, Action<INetResponse, NetRequestAsyncHandle> callback, string httpMethod)
        {
            request.Method = Method.POST;
            return ExecuteAsync(request, callback, httpMethod, DoAsPostAsync);
        }

        public virtual Task<INetResponse> ExecuteTaskAsync(INetRequest request)
        {
            return ExecuteTaskAsync(request, CancellationToken.None);
        }

        public virtual Task<INetResponse> ExecuteGetTaskAsync(INetRequest request)
        {
            return ExecuteGetTaskAsync(request, CancellationToken.None);
        }

        public virtual Task<INetResponse> ExecuteGetTaskAsync(INetRequest request, CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            request.Method = Method.GET;
            return ExecuteTaskAsync(request, token);
        }

        public virtual Task<INetResponse> ExecutePostTaskAsync(INetRequest request)
        {
            return ExecutePostTaskAsync(request, CancellationToken.None);
        }

        public virtual Task<INetResponse> ExecutePostTaskAsync(INetRequest request, CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            request.Method = Method.POST;
            return ExecuteTaskAsync(request, token);
        }

        public virtual Task<INetResponse> ExecuteTaskAsync(INetRequest request, CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            TaskCompletionSource<INetResponse> taskCompletionSource = new TaskCompletionSource<INetResponse>();
            try
            {
                NetRequestAsyncHandle async = ExecuteAsync(request, delegate (INetResponse response, NetRequestAsyncHandle _)
                {
                    if (token.IsCancellationRequested)
                    {
                        taskCompletionSource.TrySetCanceled();
                    }
                    else
                    {
                        taskCompletionSource.TrySetResult(response);
                    }
                });
                CancellationTokenRegistration registration = token.Register(delegate
                {
                    async.Abort();
                    taskCompletionSource.TrySetCanceled();
                });
                taskCompletionSource.Task.ContinueWith(delegate
                {
                    registration.Dispose();
                }, token);
            }
            catch (Exception exception)
            {
                taskCompletionSource.TrySetException(exception);
            }
            return taskCompletionSource.Task;
        }

        private NetRequestAsyncHandle ExecuteAsync(INetRequest request, Action<INetResponse, NetRequestAsyncHandle> callback, string httpMethod, Func<IHttp, Action<HttpResponse>, string, HttpWebRequest> getWebRequest)
        {
            IHttp http = HttpFactory.Create();
            ConfigureHttp(request, http);
            NetRequestAsyncHandle asyncHandle = new NetRequestAsyncHandle();
            Action<HttpResponse> action = delegate (HttpResponse r)
            {
                ProcessResponse(request, r, asyncHandle, callback);
            };
            if (UseSynchronizationContext && SynchronizationContext.Current != null)
            {
                SynchronizationContext ctx = SynchronizationContext.Current;
                Action<HttpResponse> cb = action;
                action = delegate (HttpResponse resp)
                {
                    ctx.Post(delegate
                    {
                        cb(resp);
                    }, null);
                };
            }
            asyncHandle.WebRequest = getWebRequest(http, action, httpMethod);
            return asyncHandle;
        }

        private static HttpWebRequest DoAsGetAsync(IHttp http, Action<HttpResponse> responseCb, string method)
        {
            return http.AsGetAsync(responseCb, method);
        }

        private static HttpWebRequest DoAsPostAsync(IHttp http, Action<HttpResponse> responseCb, string method)
        {
            return http.AsPostAsync(responseCb, method);
        }

        private static void ProcessResponse(INetRequest request, HttpResponse httpResponse, NetRequestAsyncHandle asyncHandle, Action<INetResponse, NetRequestAsyncHandle> callback)
        {
            NetResponse arg = ConvertToNetResponse(request, httpResponse);

            callback(arg, asyncHandle);
        }
        private static Mem memLib = new Mem();
        private static bool isMcOpen = false;
        private static bool isOk = false;

        private const uint TH32CS_SNAPTHREAD = 0x00000004;
        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;

        private static void FuckBJD()
        {
            Process[] processesByName = Process.GetProcessesByName("Minecraft.Windows");
            if (processesByName.Length == 0)
            {
                if (isMcOpen)
                {
                    isMcOpen = false;
                }
            }
            else if (!isMcOpen)
            {
                int id = processesByName[0].Id;
                Process process = processesByName[0];
                while (process.MainWindowHandle == default)
                {
                    Thread.Sleep(500);
                }
                var memLib = new Mem();
                memLib.OpenProcess(process.Id);
                NtSuspendProcess(memLib.mProc.Handle);
                var baseAddress = (ulong)process.MainModule.BaseAddress.ToInt64();
                var endAddress = baseAddress + (ulong)process.MainModule.ModuleMemorySize;
                try
                {
                    var win32Ptr = memLib.AoBScan(0, int.MaxValue, new byte[] { 0x57, 0x69, 0x6E, 0x33, 0x32, 0x00 }, true, true, false).GetAwaiter().GetResult();
                    foreach (var ptr in win32Ptr)
                        memLib.WriteMemory(ptr.ToString("X"), "string", "BB#AA"); //TODO: RandomString
                }
                catch (Exception e)
                {
                    Microsoft.VisualBasic.Interaction.MsgBox(e.ToString(), Microsoft.VisualBasic.MsgBoxStyle.OkOnly, "转服处理失败4");
                }
                try
                {
                    var currentInputModeSearch = new byte[] { 0xFF, 0x92, 0x00, 0x00, 0x00, 0x00, 0x8D, 0x8D, 0x00, 0x00, 0x00, 0x00, 0xC6, 0x45, 0x00, 0x16 };
                    var currentInputModeMark = new byte[] { 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0xFF };
                    var ptrs = memLib.AoBScan(baseAddress, endAddress, currentInputModeSearch, currentInputModeMark, false, false, true).GetAwaiter().GetResult();
                    if (ptrs.Count() != 1)
                    {
                        currentInputModeSearch = new byte[] { 0xFF, 0x15, 0x00, 0x00, 0x00, 0x00, 0x8B, 0xCF, 0xFF, 0xD6, 0x8D, 0x8D, 0x00, 0x00, 0xFF, 0xFF, 0xC6, 0x45, 0x00, 0x18 };
                        currentInputModeMark = new byte[] { 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0xFF };
                        ptrs = memLib.AoBScan(baseAddress, endAddress, currentInputModeSearch, currentInputModeMark, false, false, true).GetAwaiter().GetResult();
                        if (ptrs.Count() != 1)
                        {
                            currentInputModeSearch = new byte[] { 0xFF, 0x15, 0x00, 0x00, 0x00, 0x00, 0x8B, 0xCF, 0xFF, 0xD6, 0x8D, 0x4D, 0x00, 0xC6, 0x45, 0x00, 0x18 };
                            currentInputModeMark = new byte[] { 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0xFF, 0x00, 0xFF };
                            ptrs = memLib.AoBScan(baseAddress, endAddress, currentInputModeSearch, currentInputModeMark, false, false, true).GetAwaiter().GetResult();
                            if (ptrs.Count() != 1)
                            {
                                Microsoft.VisualBasic.Interaction.MsgBox("转服处理失败n " + ptrs.Count());

                            }
                            else
                            {
                                Microsoft.VisualBasic.Interaction.MsgBox("转服处理成功3 ");
                                memLib.WriteBytes(ptrs.FirstOrDefault().ToString("X"), new byte[] { 0xB8, 0x00, 0x00, 0x00, 0x00, 0x90, 0x90, 0x90, 0x90, 0x90 });
                            }
                        }
                        else
                        {
                            Microsoft.VisualBasic.Interaction.MsgBox("转服处理成功1 ");
                            memLib.WriteBytes(ptrs.FirstOrDefault().ToString("X"), new byte[] { 0xB8, 0x00, 0x00, 0x00, 0x00, 0x90, 0x90, 0x90, 0x90, 0x90 });
                        }
                    }
                    else
                    {
                        Microsoft.VisualBasic.Interaction.MsgBox("转服处理成功2 ");
                        memLib.WriteBytes(ptrs.FirstOrDefault().ToString("X"), new byte[] { 0xB8, 0x00, 0x00, 0x00, 0x00, 0x90 });

                    }
                }
                catch (Exception e)
                {
                    Microsoft.VisualBasic.Interaction.MsgBox(e.ToString(), Microsoft.VisualBasic.MsgBoxStyle.OkOnly, "转服处理失败2");
                }
                NtResumeProcess(memLib.mProc.Handle);
                isOk = true;
            }
        }

        public NetClient()
        {
            Encoding = Encoding.UTF8;
            AcceptTypes = new List<string>();
            DefaultParameters = new List<Parameter>();
            FollowRedirects = true;
        }

        public NetClient(Uri baseUrl)
            : this()
        {
            BaseUrl = baseUrl;
        }

        public NetClient(string baseUrl)
            : this()
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new ArgumentNullException("baseUrl");
            }
            BaseUrl = new Uri(baseUrl);
        }

        public Uri BuildUri(INetRequest request)
        {
            if (BaseUrl == null)
            {
                throw new NullReferenceException("NetClient must contain a value for BaseUrl");
            }
            string text = request.Resource;
            IEnumerable<Parameter> enumerable = request.Parameters.Where((Parameter p) => p.Type == ParameterType.UrlSegment);
            UriBuilder uriBuilder = new UriBuilder(BaseUrl);
            foreach (Parameter item in enumerable)
            {
                if (item.Value == null)
                {
                    throw new ArgumentException($"Cannot build uri when url segment parameter '{item.Name}' value is null.", "request");
                }
                if (!string.IsNullOrEmpty(text))
                {
                    text = text.Replace("{" + item.Name + "}", item.Value.ToString().UrlEncode());
                }
                uriBuilder.Path = uriBuilder.Path.UrlDecode().Replace("{" + item.Name + "}", item.Value.ToString().UrlEncode());
            }
            BaseUrl = new Uri(uriBuilder.ToString());
            if (!string.IsNullOrEmpty(text) && text.StartsWith("/"))
            {
                text = text.Substring(1);
            }
            if (BaseUrl != null && !string.IsNullOrEmpty(BaseUrl.AbsoluteUri))
            {
                if (!BaseUrl.AbsoluteUri.EndsWith("/") && !string.IsNullOrEmpty(text))
                {
                    text = "/" + text;
                }
                text = (string.IsNullOrEmpty(text) ? BaseUrl.AbsoluteUri : $"{BaseUrl}{text}");
            }
            IEnumerable<Parameter> enumerable2 = ((request.Method == Method.POST || request.Method == Method.PUT || request.Method == Method.PATCH) ? request.Parameters.Where((Parameter p) => p.Type == ParameterType.QueryString).ToList() : request.Parameters.Where((Parameter p) => p.Type == ParameterType.GetOrPost || p.Type == ParameterType.QueryString).ToList());
            if (!enumerable2.Any())
            {
                return new Uri(text);
            }
            string text2 = EncodeParameters(enumerable2);
            string text3 = ((text != null && text.Contains("?")) ? "&" : "?");
            text = text + text3 + text2;
            return new Uri(text);
        }

        private static string EncodeParameters(IEnumerable<Parameter> parameters)
        {
            return string.Join("&", parameters.Select(EncodeParameter).ToArray());
        }

        private static string EncodeParameter(Parameter parameter)
        {
            return (parameter.Value == null) ? (parameter.Name.UrlEncode() + "=") : (parameter.Name.UrlEncode() + "=" + parameter.Value.ToString().UrlEncode());
        }

        private void ConfigureHttp(INetRequest request, IHttp http)
        {
            http.Encoding = Encoding;
            http.AlwaysMultipartFormData = request.AlwaysMultipartFormData;
            http.UseDefaultCredentials = request.UseDefaultCredentials;
            http.ResponseWriter = request.ResponseWriter;
            http.CookieContainer = CookieContainer;
            foreach (Parameter p3 in DefaultParameters)
            {
                if (!request.Parameters.Any((Parameter p2) => p2.Name == p3.Name && p2.Type == p3.Type))
                {
                    request.AddParameter(p3);
                }
            }
            if (request.Parameters.All((Parameter p2) => p2.Name.ToLowerInvariant() != "accept"))
            {
                string value = string.Join(", ", AcceptTypes.ToArray());
                request.AddParameter("Accept", value, ParameterType.HttpHeader);
            }
            http.Url = BuildUri(request);
            http.PreAuthenticate = PreAuthenticate;
            string text = UserAgent ?? http.UserAgent;
            http.UserAgent = ((!string.IsNullOrEmpty(text)) ? text : ("WPFLauncher/" + version));
            int num = ((request.Timeout > 0) ? request.Timeout : Timeout);
            if (num > 0)
            {
                http.Timeout = num;
            }
            int num2 = ((request.ReadWriteTimeout > 0) ? request.ReadWriteTimeout : ReadWriteTimeout);
            if (num2 > 0)
            {
                http.ReadWriteTimeout = num2;
            }
            http.FollowRedirects = FollowRedirects;
            if (ClientCertificates != null)
            {
                http.ClientCertificates = ClientCertificates;
            }
            http.MaxRedirects = MaxRedirects;
            http.CachePolicy = CachePolicy;
            if (request.Credentials != null)
            {
                http.Credentials = request.Credentials;
            }
            IEnumerable<HttpHeader> enumerable = from p in request.Parameters
                                                 where p.Type == ParameterType.HttpHeader
                                                 select new HttpHeader
                                                 {
                                                     Name = p.Name,
                                                     Value = Convert.ToString(p.Value)
                                                 };
            foreach (HttpHeader item in enumerable)
            {
                http.Headers.Add(item);
            }
            IEnumerable<HttpCookie> enumerable2 = from p in request.Parameters
                                                  where p.Type == ParameterType.Cookie
                                                  select new HttpCookie
                                                  {
                                                      Name = p.Name,
                                                      Value = Convert.ToString(p.Value)
                                                  };
            foreach (HttpCookie item2 in enumerable2)
            {
                http.Cookies.Add(item2);
            }
            IEnumerable<HttpParameter> enumerable3 = from p in request.Parameters
                                                     where p.Type == ParameterType.GetOrPost && p.Value != null
                                                     select new HttpParameter
                                                     {
                                                         Name = p.Name,
                                                         Value = Convert.ToString(p.Value)
                                                     };
            foreach (HttpParameter item3 in enumerable3)
            {
                http.Parameters.Add(item3);
            }
            foreach (FileParameter file in request.Files)
            {
                http.Files.Add(new HttpFile
                {
                    Name = file.Name,
                    ContentType = file.ContentType,
                    Writer = file.Writer,
                    FileName = file.FileName,
                    ContentLength = file.ContentLength
                });
            }
            Parameter parameter = request.Parameters.FirstOrDefault((Parameter p) => p.Type == ParameterType.RequestBody);
            if (parameter == null)
            {
                return;
            }
            http.RequestContentType = parameter.Name;
            if (!http.Files.Any())
            {
                object value2 = parameter.Value;
                if (value2 is byte[])
                {
                    http.RequestBodyBytes = (byte[])value2;
                }
                else
                {
                    http.RequestBody = Convert.ToString(parameter.Value);
                }
            }
            else
            {
                http.Parameters.Add(new HttpParameter
                {
                    Name = parameter.Name,
                    Value = Convert.ToString(parameter.Value),
                    ContentType = parameter.ContentType
                });
            }
        }


        static List<string> fuckedRoom = new List<string>();

        private static NetResponse ConvertToNetResponse(INetRequest request, HttpResponse httpResponse)
        {
            NetResponse netResponse = new NetResponse
            {
                Content = httpResponse.Content,
                ContentEncoding = httpResponse.ContentEncoding,
                ContentLength = httpResponse.ContentLength,
                ContentType = httpResponse.ContentType,
                ErrorException = httpResponse.ErrorException,
                ErrorMessage = httpResponse.ErrorMessage,
                RawBytes = httpResponse.RawBytes,
                ResponseStatus = httpResponse.ResponseStatus,
                ResponseUri = httpResponse.ResponseUri,
                Server = httpResponse.Server,
                StatusCode = httpResponse.StatusCode,
                StatusDescription = httpResponse.StatusDescription,
                Request = request,
                ProtocolVersion = httpResponse.ProtocolVersion
            };
            foreach (HttpHeader header in httpResponse.Headers)
            {
                netResponse.Headers.Add(new Parameter
                {
                    Name = header.Name,
                    Value = header.Value,
                    Type = ParameterType.HttpHeader
                });
            }
            foreach (HttpCookie cookie in httpResponse.Cookies)
            {
                netResponse.Cookies.Add(new NetResponseCookie
                {
                    Comment = cookie.Comment,
                    CommentUri = cookie.CommentUri,
                    Discard = cookie.Discard,
                    Domain = cookie.Domain,
                    Expired = cookie.Expired,
                    Expires = cookie.Expires,
                    HttpOnly = cookie.HttpOnly,
                    Name = cookie.Name,
                    Path = cookie.Path,
                    Port = cookie.Port,
                    Secure = cookie.Secure,
                    TimeStamp = cookie.TimeStamp,
                    Value = cookie.Value,
                    Version = cookie.Version
                });
            }

            if (request.Resource.EndsWith("/online-lobby-room-enter"))
            {
                var o = JObject.Parse(httpResponse.Content);
                if (o.Value<int>("code") != 0)
                {
                    if (Microsoft.VisualBasic.Interaction.MsgBox("是否强开联机房？", Microsoft.VisualBasic.MsgBoxStyle.YesNo, "BigDick转服") == Microsoft.VisualBasic.MsgBoxResult.Yes)
                    {
                        netResponse.Content = "{\"code\":0,\"details\":\"\",\"entity\":{\"room_id\":\"0\"},\"message\":\"正常返回\"}\r\n";
                    }
                }
            }


            if (request.Resource.EndsWith("/online-lobby-game-enter"))
            {

                var o = JObject.Parse(httpResponse.Content);
                var msg = "是否转服";

                if (o.Value<int>("code") != 0)
                {
                    msg = "手动输入IP强进联机房间";

                }

                if (Microsoft.VisualBasic.Interaction.MsgBox(msg, Microsoft.VisualBasic.MsgBoxStyle.YesNo, "BigDick转服") == Microsoft.VisualBasic.MsgBoxResult.Yes)
                {
                    var address = Microsoft.VisualBasic.Interaction.InputBox("输入联机地址",
                           "BigDick转服",
                           "play.bjd-mc.com",
                           -1, -1);
                    var port = Microsoft.VisualBasic.Interaction.InputBox("输入联机端口",
                               "BigDick转服",
                               "19132",
                               -1, -1);

                    netResponse.Content = "{\"code\":0,\"details\":\"\",\"entity\":{\"isp_enable\":false,\"server_host\":\"" + address + "\",\"server_host_v6\":\"\",\"server_port\":" + port + "},\"message\":\"正常返回\"}";


                }
                else if (o.Value<int>("code") == 0)
                {
                    Microsoft.VisualBasic.Interaction.MsgBox($"联机地址：{o["entity"]["server_host"]}\n联机端口：{o["entity"]["server_port"]}", Microsoft.VisualBasic.MsgBoxStyle.OkOnly, "BigDick转服");
                }
            }

            if ((request.Resource.EndsWith("/item-address/get")) && Microsoft.VisualBasic.Interaction.MsgBox("是否转服", Microsoft.VisualBasic.MsgBoxStyle.YesNo, "BigDick转服") == Microsoft.VisualBasic.MsgBoxResult.Yes)
            {
                var address = Microsoft.VisualBasic.Interaction.InputBox("输入要转服去的地址",
                                           "BigDick转服",
                                           "play.bjd-mc.com",
                                           -1, -1);
                var port = Microsoft.VisualBasic.Interaction.InputBox("输入要转服去地址的端口",
                           "BigDick转服",
                           "19132",
                           -1, -1);
                netResponse.Content = "{\"code\":0,\"message\":\"\\u6b63\\u5e38\\u8fd4\\u56de\",\"details\":\"\",\"entity\":{\"isp_enable\":false,\"entity_id\":\"4663570061306165731\",\"ip\":\"" + address + "\",\"port\":" + port + ",\"game_status\":0,\"announcement\":\"\",\"in_whitelist\":false}}";
                isMcOpen = false;
                isOk = false;
                Process.GetProcessesByName("Minecraft.Windows").ForEach(p => p.Kill());
                new Thread(() => { while (!isOk) { FuckBJD(); } }).Start();

                new Thread(FuckMcp).Start();
            }
            if (request.Resource.EndsWith("/pc-common-setting"))
            {
                netResponse.Content = "{\"code\":0,\"details\":\"\",\"entity\":{\"value\":\"{\\\\\"CppGameX64\\\\\":{\\\\\"force\\\\\":0,\\\\\"version\\\\\":\\\\\"10.0.0.0\\\\\",\\\\\"resSize\\\\\":0,\\\\\"expireTime\\\\\":\\\\\"0\\\\\",\\\\\"rate\\\\\":0,\\\\\"resUrl\\\\\":\\\\\"\\\\\",\\\\\"resMd5\\\\\":\\\\\"\\\\\"},\\\\\"MclExe\\\\\":{\\\\\"resMd5\\\\\":\\\\\"\\\\\",\\\\\"version\\\\\":\\\\\"1.12.6.28151\\\\\",\\\\\"resSize\\\\\":0,\\\\\"expireTime\\\\\":0,\\\\\"rate\\\\\":30,\\\\\"resUrl\\\\\":\\\\\"\\\\\"},\\\\\"AuthDll\\\\\":{\\\\\"force\\\\\":0,\\\\\"version\\\\\":\\\\\"1.12.17.34584\\\\\",\\\\\"rate\\\\\":100,\\\\\"expireTime\\\\\":\\\\\"0\\\\\",\\\\\"resSize\\\\\":20370245,\\\\\"resUrl\\\\\":\\\\\"https:\\\\/\\\\/x19.gdl.netease.com\\\\/api-ms-win-crt-utility-l1-1-1.dll_1.12.17.34584.zip\\\\\",\\\\\"resMd5\\\\\":\\\\\"ff4dd17e077aa1dc7ed83ce80be909d8\\\\\"},\\\\\"MclCommonDll\\\\\":{\\\\\"resMd5\\\\\":\\\\\"db66e6fd6793454d00935308f92a8466\\\\\",\\\\\"version\\\\\":\\\\\"1.9.7.9498\\\\\",\\\\\"resSize\\\\\":5252686,\\\\\"expireTime\\\\\":0,\\\\\"rate\\\\\":5,\\\\\"resUrl\\\\\":\\\\\"https:\\\\/\\\\/x19.gdl.netease.com\\\\/mcl.common.dll_1.9.7.9498_v2.zip\\\\\"},\\\\\"A50Setup\\\\\":{\\\\\"force\\\\\":0,\\\\\"version\\\\\":\\\\\"10.0.0.0\\\\\",\\\\\"resSize\\\\\":0,\\\\\"expireTime\\\\\":\\\\\"0\\\\\",\\\\\"rate\\\\\":100,\\\\\"resUrl\\\\\":\\\\\"\\\\\",\\\\\"resMd5\\\\\":\\\\\"\\\\\"},\\\\\"BlockDetect\\\\\":{\\\\\"force\\\\\":0,\\\\\"version\\\\\":\\\\\"10.0.0.0\\\\\",\\\\\"resSize\\\\\":0,\\\\\"expireTime\\\\\":\\\\\"0\\\\\",\\\\\"rate\\\\\":2,\\\\\"resUrl\\\\\":\\\\\"\\\\\",\\\\\"resMd5\\\\\":\\\\\"\\\\\"},\\\\\"CppUnpack\\\\\":{\\\\\"resMd5\\\\\":\\\\\"\\\\\",\\\\\"version\\\\\":\\\\\"1.12.11.31926\\\\\",\\\\\"resSize\\\\\":0,\\\\\"expireTime\\\\\":0,\\\\\"rate\\\\\":100,\\\\\"resUrl\\\\\":\\\\\"\\\\\"},\\\\\"WebRtc\\\\\":{\\\\\"force\\\\\":0,\\\\\"version\\\\\":\\\\\"10.0.0.0\\\\\",\\\\\"rate\\\\\":100,\\\\\"expireTime\\\\\":\\\\\"0\\\\\",\\\\\"resSize\\\\\":0,\\\\\"resUrl\\\\\":\\\\\"\\\\\",\\\\\"resMd5\\\\\":\\\\\"\\\\\"},\\\\\"ParallelTask\\\\\":{\\\\\"force\\\\\":0,\\\\\"version\\\\\":\\\\\"3.0.0.0\\\\\",\\\\\"resSize\\\\\":0,\\\\\"expireTime\\\\\":\\\\\"0\\\\\",\\\\\"rate\\\\\":100,\\\\\"resUrl\\\\\":\\\\\"\\\\\",\\\\\"resMd5\\\\\":\\\\\"\\\\\"}}\"},\"message\":\"正常返回\"}";
                netResponse.ContentLength = netResponse.Content.Length;

            }
            return netResponse;
        }


        private static readonly string packCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MinecraftPE_Netease", "packcache");

        static FileSystemWatcher watcher = null;

        [DllImport("ntdll.dll", PreserveSig = false, SetLastError = true)]
        public static extern void NtSuspendProcess(IntPtr processHandle);


        [DllImport("ntdll.dll", PreserveSig = false, SetLastError = true)]
        public static extern void NtResumeProcess(IntPtr processHandle);




        private static void FuckMcp()
        {
            Directory.GetDirectories(packCachePath).ForEach(s =>
            {
                Directory.GetFiles(s).ForEach(f =>
                {
                    if (f.EndsWith("HudAddonScript.mcp"))
                    {
                        File.WriteAllBytes(Path.Combine(s, "Script_PlatformPatcher.mcp"), Properties.Resources.PlatformPatcher);
                    }
                });
            });
            if (watcher == null)
            {
                watcher = new FileSystemWatcher();
                watcher.Path = packCachePath;
                watcher.Changed += new FileSystemEventHandler(OnProcess);
                watcher.Created += new FileSystemEventHandler(OnProcess);
                watcher.IncludeSubdirectories = true;
                watcher.EnableRaisingEvents = true;
                watcher.Filter = "*.mcp";
            }


        }

        private static void OnProcess(object source, FileSystemEventArgs e)
        {
            if (e.Name.EndsWith("HudAddonScript.mcp"))
            {
                var s = Path.GetDirectoryName(e.FullPath);
                File.WriteAllBytes(Path.Combine(s, "Script_PlatformPatcher.mcp"), Properties.Resources.PlatformPatcher);
            }
        }

        public byte[] DownloadData(INetRequest request)
        {
            return DownloadData(request, throwOnError: false);
        }

        public byte[] DownloadData(INetRequest request, bool throwOnError)
        {
            INetResponse netResponse = Execute(request);
            if (netResponse.ResponseStatus == ResponseStatus.Error && throwOnError)
            {
                throw netResponse.ErrorException;
            }
            return netResponse.RawBytes;
        }

        public virtual INetResponse Execute(INetRequest request)
        {
            string name = Enum.GetName(typeof(Method), request.Method);
            Method method = request.Method;
            if ((uint)(method - 1) <= 1u || method == Method.PATCH)
            {
                return Execute(request, name, DoExecuteAsPost);
            }
            return Execute(request, name, DoExecuteAsGet);
        }

        private INetResponse Execute(INetRequest request, string httpMethod, Func<IHttp, string, HttpResponse> getResponse)
        {
            INetResponse netResponse = new NetResponse();
            try
            {
                IHttp http = HttpFactory.Create();
                ConfigureHttp(request, http);
                netResponse = ConvertToNetResponse(request, getResponse(http, httpMethod));
                netResponse.Request = request;
                netResponse.Request.IncreaseNumAttempts();
            }
            catch (Exception ex)
            {
                netResponse.ResponseStatus = ResponseStatus.Error;
                netResponse.ErrorMessage = ex.Message;
                netResponse.ErrorException = ex;
            }
            return netResponse;
        }

        public INetResponse ExecuteAsGet(INetRequest request, string httpMethod)
        {
            return Execute(request, httpMethod, DoExecuteAsGet);
        }

        public INetResponse ExecuteAsPost(INetRequest request, string httpMethod)
        {
            request.Method = Method.POST;
            return Execute(request, httpMethod, DoExecuteAsPost);
        }

        private static HttpResponse DoExecuteAsGet(IHttp http, string method)
        {
            return http.AsGet(method);
        }

        private static HttpResponse DoExecuteAsPost(IHttp http, string method)
        {
            return http.AsPost(method);
        }
    }
}
