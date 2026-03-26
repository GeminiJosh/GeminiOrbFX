using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace GeminiOrbFX.UI.Services
{
    internal class TikTokLiveClientWrapper
    {
        private object _client;
        private Type _clientType;

        private Action<string> _onFollow;
        private Action<string, int> _onGift;
        private Action<string, string> _onChat;

        public bool IsConnected { get; private set; }

        public void Connect(
            string username,
            Action<string> onFollow,
            Action<string, int> onGift,
            Action<string, string> onChat)
        {
            Disconnect();

            _onFollow = onFollow;
            _onGift = onGift;
            _onChat = onChat;

            _clientType = FindClientType();
            if (_clientType == null)
                throw new Exception("Could not find TikTokLiveClient type in TikTokLiveSharp assembly.");

            _client = CreateClientInstance(_clientType, username);
            if (_client == null)
                throw new Exception("Failed to create TikTokLiveClient instance.");

            bool hookedAny =
                HookEvent("OnFollow", HandleFollow) |
                HookEvent("OnGift", HandleGift) |
                HookEvent("OnComment", HandleChat) |
                HookEvent("OnChat", HandleChat) |
                HookEvent("OnChatMessage", HandleChat) |
                HookEvent("OnCommentMessage", HandleChat);

            if (!hookedAny)
                Plugin.Log.Warn("[GeminiOrbFX UI] No TikTokLiveSharp events were hooked. Event names may differ in this build.");

            bool connected = TryInvokeLifecycleMethod("Connect")
                          || TryInvokeLifecycleMethod("Run")
                          || TryInvokeLifecycleMethod("Start")
                          || TryInvokeLifecycleMethod("ConnectAsync");

            if (!connected)
                throw new Exception("Could not find a usable connect/start method on TikTokLiveClient.");

            IsConnected = true;
        }

        public void Disconnect()
        {
            try
            {
                if (_client != null)
                {
                    TryInvokeLifecycleMethod("Disconnect");
                    TryInvokeLifecycleMethod("Stop");
                    TryInvokeLifecycleMethod("Dispose");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn("[GeminiOrbFX UI] TikTok wrapper disconnect warning: " + ex);
            }

            _client = null;
            _clientType = null;
            IsConnected = false;
        }

        private Type FindClientType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string asmName = asm.GetName().Name ?? string.Empty;

                if (!asmName.ToLowerInvariant().Contains("tiktok"))
                    continue;

                Plugin.Log.Info("[GeminiOrbFX UI] TikTok wrapper: inspecting assembly " + asmName);

                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    Plugin.Log.Info("[GeminiOrbFX UI] TikTok wrapper type: " + t.FullName);
                }

                // try exact names first
                var exact = types.FirstOrDefault(t =>
                    t.FullName == "TikTokLiveSharp.TikTokLiveClient" ||
                    t.FullName == "TikTokLiveSharp.Client.TikTokLiveClient" ||
                    t.FullName == "TikTokLiveSharp.Clients.TikTokLiveClient");

                if (exact != null)
                    return exact;

                // fallback: anything that looks like the client
                var fuzzy = types.FirstOrDefault(t =>
                    t.Name.ToLowerInvariant().Contains("client") &&
                    t.FullName.ToLowerInvariant().Contains("tiktok"));

                if (fuzzy != null)
                    return fuzzy;
            }

            return null;
        }

        private object CreateClientInstance(Type clientType, string username)
        {
            var ctors = clientType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                  .OrderBy(c => c.GetParameters().Length)
                                  .ToArray();

            for (int i = 0; i < ctors.Length; i++)
            {
                var ctor = ctors[i];
                var parameters = ctor.GetParameters();

                if (parameters.Length == 0)
                    continue;

                if (parameters[0].ParameterType != typeof(string))
                    continue;

                try
                {
                    object[] args = BuildConstructorArgs(parameters, username);
                    return ctor.Invoke(args);
                }
                catch
                {
                    // try next constructor
                }
            }

            return null;
        }

        private object[] BuildConstructorArgs(ParameterInfo[] parameters, string username)
        {
            var args = new object[parameters.Length];
            args[0] = username;

            for (int i = 1; i < parameters.Length; i++)
            {
                var p = parameters[i];

                if (p.HasDefaultValue)
                {
                    args[i] = p.DefaultValue;
                    continue;
                }

                Type t = p.ParameterType;

                if (t == typeof(string))
                    args[i] = null;
                else if (t == typeof(bool))
                    args[i] = false;
                else if (t == typeof(int) || t == typeof(uint) || t == typeof(short) || t == typeof(byte))
                    args[i] = 0;
                else if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
                    args[i] = 0;
                else if (Nullable.GetUnderlyingType(t) != null)
                    args[i] = null;
                else if (!t.IsValueType)
                    args[i] = null;
                else
                    args[i] = Activator.CreateInstance(t);
            }

            return args;
        }

        private bool HookEvent(string eventName, Action<object, object> callback)
        {
            if (_clientType == null || _client == null)
                return false;

            var evt = _clientType.GetEvent(eventName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (evt == null)
                return false;

            try
            {
                Delegate handler = CreateDelegate(evt.EventHandlerType, callback);
                evt.AddEventHandler(_client, handler);
                Plugin.Log.Info("[GeminiOrbFX UI] Hooked TikTok event: " + eventName);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn("[GeminiOrbFX UI] Failed hooking event " + eventName + ": " + ex.Message);
                return false;
            }
        }

        private Delegate CreateDelegate(Type eventHandlerType, Action<object, object> callback)
        {
            MethodInfo invoke = eventHandlerType.GetMethod("Invoke");
            ParameterInfo[] parameters = invoke.GetParameters();

            if (parameters.Length != 2)
                throw new NotSupportedException("Only 2-parameter event handlers are supported.");

            ParameterExpression senderParam = Expression.Parameter(parameters[0].ParameterType, "sender");
            ParameterExpression eventParam = Expression.Parameter(parameters[1].ParameterType, "evt");

            MethodInfo callbackInvoke = typeof(Action<object, object>).GetMethod("Invoke");

            MethodCallExpression body = Expression.Call(
                Expression.Constant(callback),
                callbackInvoke,
                Expression.Convert(senderParam, typeof(object)),
                Expression.Convert(eventParam, typeof(object)));

            return Expression.Lambda(eventHandlerType, body, senderParam, eventParam).Compile();
        }

        private bool TryInvokeLifecycleMethod(string methodName)
        {
            if (_clientType == null || _client == null)
                return false;

            var methods = _clientType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                     .Where(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase))
                                     .ToArray();

            for (int i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                var parameters = method.GetParameters();

                try
                {
                    if (parameters.Length == 0)
                    {
                        method.Invoke(_client, null);
                        return true;
                    }

                    if (parameters.Length == 1)
                    {
                        object arg = BuildSingleMethodArg(parameters[0].ParameterType);
                        method.Invoke(_client, new object[] { arg });
                        return true;
                    }
                }
                catch
                {
                    // try next overload
                }
            }

            return false;
        }

        private object BuildSingleMethodArg(Type type)
        {
            if (!type.IsValueType)
                return null;

            if (Nullable.GetUnderlyingType(type) != null)
                return null;

            return Activator.CreateInstance(type);
        }

        private void HandleFollow(object sender, object evt)
        {
            string username = ExtractUsername(evt);
            if (!string.IsNullOrWhiteSpace(username))
                _onFollow?.Invoke(username);
        }

        private void HandleGift(object sender, object evt)
        {
            string username = ExtractUsername(evt);
            int amount = ExtractInt(evt,
                "RepeatCount",
                "ComboCount",
                "Count",
                "Amount",
                "DiamondCount",
                "CoinCount",
                "TotalCoins",
                "TotalCoin");

            if (amount <= 0)
                amount = 1;

            if (!string.IsNullOrWhiteSpace(username))
                _onGift?.Invoke(username, amount);
        }

        private void HandleChat(object sender, object evt)
        {
            string username = ExtractUsername(evt);
            string message = ExtractString(evt,
                "Comment",
                "Message",
                "Text",
                "Content");

            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(message))
                _onChat?.Invoke(username, message);
        }

        private string ExtractUsername(object evt)
        {
            return ExtractStringPath(evt,
                "User.UniqueId",
                "User.UniqueID",
                "User.Username",
                "User.Nickname",
                "Sender.UniqueId",
                "Sender.UniqueID",
                "Sender.Username",
                "UniqueId",
                "UniqueID",
                "Username");
        }

        private int ExtractInt(object obj, params string[] propertyNames)
        {
            if (obj == null)
                return 0;

            for (int i = 0; i < propertyNames.Length; i++)
            {
                object value = GetMemberValue(obj, propertyNames[i]);
                if (value == null)
                    continue;

                try
                {
                    return Convert.ToInt32(value);
                }
                catch
                {
                }
            }

            return 0;
        }

        private string ExtractString(object obj, params string[] propertyNames)
        {
            if (obj == null)
                return null;

            for (int i = 0; i < propertyNames.Length; i++)
            {
                object value = GetMemberValue(obj, propertyNames[i]);
                if (value == null)
                    continue;

                string str = value.ToString();
                if (!string.IsNullOrWhiteSpace(str))
                    return str;
            }

            return null;
        }

        private string ExtractStringPath(object obj, params string[] paths)
        {
            if (obj == null)
                return null;

            for (int i = 0; i < paths.Length; i++)
            {
                object value = GetPathValue(obj, paths[i]);
                if (value == null)
                    continue;

                string str = value.ToString();
                if (!string.IsNullOrWhiteSpace(str))
                    return str;
            }

            return null;
        }

        private object GetPathValue(object obj, string path)
        {
            if (obj == null || string.IsNullOrWhiteSpace(path))
                return null;

            string[] parts = path.Split('.');
            object current = obj;

            for (int i = 0; i < parts.Length; i++)
            {
                if (current == null)
                    return null;

                current = GetMemberValue(current, parts[i]);
            }

            return current;
        }

        private object GetMemberValue(object obj, string memberName)
        {
            if (obj == null || string.IsNullOrWhiteSpace(memberName))
                return null;

            Type type = obj.GetType();

            var prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
                return prop.GetValue(obj, null);

            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(obj);

            return null;
        }
    }
}