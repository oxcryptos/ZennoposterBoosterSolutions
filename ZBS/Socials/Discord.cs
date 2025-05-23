﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using ZennoLab.CommandCenter;
using ZennoLab.InterfacesLibrary.ProjectModel;
using Newtonsoft.Json.Linq;
namespace ZBSolutions
{
    public class Discord
    {
        protected readonly IZennoPosterProjectModel _project;
        protected readonly Instance _instance;
        protected readonly bool _logShow;
        protected readonly string _pass;
        protected readonly Sql _sql;
        protected readonly NetHttp _http;
        public Discord(IZennoPosterProjectModel project, Instance instance, bool log = false)
        {
            _project = project;
            _instance = instance;
            _logShow = log;
            _sql = new Sql(_project);
            _http = new NetHttp(_project);
        }
        public void Log(string tolog = "", [CallerMemberName] string callerName = "", bool log = false)
        {
            if (!_logShow && !log) return;
            var stackFrame = new System.Diagnostics.StackFrame(1);
            var callingMethod = stackFrame.GetMethod();
            if (callingMethod == null || callingMethod.DeclaringType == null || callingMethod.DeclaringType.FullName.Contains("Zenno")) callerName = "null";
            _project.L0g($"[ 👾  {callerName}] [{tolog}] ");
        }

        public bool ManageRole(string botToken, string guildId, string roleName, string userId, bool assignRole, [CallerMemberName] string callerName = "")
        {
            try
            {
                // Заголовки для авторизации
                var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bot {botToken}" },
            { "User-Agent", "DiscordBot/1.0" } // Явно задаём User-Agent, чтобы Discord не блочил
        };
                Log($"Заголовки для запроса: {string.Join(", ", headers.Select(h => $"{h.Key}: {h.Value}"))}", callerName);

                // 1. Получаем список ролей сервера, чтобы найти ID роли по имени
                string rolesUrl = $"https://discord.com/api/v10/guilds/{guildId}/roles";
                Log($"Отправляем GET: {rolesUrl}", callerName);
                string rolesResponse = _http.GET(rolesUrl, headers: headers, callerName: callerName);

                Log($"Ответ от GET: {rolesResponse}", callerName);
                if (rolesResponse.StartsWith("Ошибка"))
                {
                    Log($"!W Не удалось получить роли сервера: {rolesResponse}", callerName, true);
                    return false;
                }

                // Парсим JSON для поиска роли
                JArray roles = JArray.Parse(rolesResponse);
                var role = roles.FirstOrDefault(r => r["name"].ToString().Equals(roleName, StringComparison.OrdinalIgnoreCase));
                if (role == null)
                {
                    Log($"!W Роль с именем '{roleName}' не найдена на сервере", callerName, true);
                    return false;
                }
                string roleId = role["id"].ToString();
                Log($"Найдена роль: {roleName} (ID: {roleId})", callerName);

                // 2. Формируем URL для выдачи или удаления роли
                string url = $"https://discord.com/api/v10/guilds/{guildId}/members/{userId}/roles/{roleId}";

                // 3. Выполняем запрос в зависимости от assignRole
                string result;
                if (assignRole)
                {
                    Log($"Отправляем PUT: {url}", callerName);
                    result = _http.PUT(url, "", proxyString: null, headers: headers, callerName: callerName);
                }
                else
                {
                    Log($"Отправляем DELETE: {url}", callerName);
                    result = _http.DELETE(url, proxyString: null, headers: headers, callerName: callerName);
                }

                Log($"Ответ от {(assignRole ? "PUT" : "DELETE")}: {result}", callerName);
                if (result.StartsWith("Ошибка"))
                {
                    Log($"!W Не удалось {(assignRole ? "выдать" : "удалить")} роль: {result}", callerName, true);
                    return false;
                }

                Log($"{(assignRole ? "Роль успешно выдана" : "Роль успешно удалена")}: {roleName} для пользователя {userId}", callerName);
                return true;
            }
            catch (Exception e)
            {
                Log($"!W Ошибка при управлении ролью: [{e.Message}]", callerName, true);
                return false;
            }
        }


        private void DSsetToken()
        {
            var jsCode = "function login(token) {\r\n    setInterval(() => {\r\n        document.body.appendChild(document.createElement `iframe`).contentWindow.localStorage.token = `\"${token}\"`\r\n    }, 50);\r\n    setTimeout(() => {\r\n        location.reload();\r\n    }, 1000);\r\n}\r\n    login(\'discordTOKEN\');\r\n".Replace("discordTOKEN", _project.Variables["discordTOKEN"].Value);
            _instance.ActiveTab.MainDocument.EvaluateScript(jsCode);
        }
        private string DSgetToken()
        {
            var token = _instance.ActiveTab.MainDocument.EvaluateScript("return (webpackChunkdiscord_app.push([[\'\'],{},e=>{m=[];for(let c in e.c)m.push(e.c[c])}]),m).find(m=>m?.exports?.default?.getToken!== void 0).exports.default.getToken();\r\n");
            return token;
        }
        private string DSlogin()
        {
            _project.SendInfoToLog("DLogin");
            DateTime deadline = DateTime.Now.AddSeconds(60);
            _instance.CloseExtraTabs();
            _instance.HeSet(("input:text", "aria-label", "Email or Phone Number", "text", 0), _project.Variables["discordLOGIN"].Value);
            _instance.HeSet(("input:password", "aria-label", "Password", "text", 0), _project.Variables["discordPASSWORD"].Value);
            _instance.HeClick(("button", "type", "submit", "regexp", 0));

            while (_instance.ActiveTab.FindElementByAttribute("div", "innertext", "Are\\ you\\ human\\?", "regexp", 0).IsVoid &&
                _instance.ActiveTab.FindElementByAttribute("input:text", "autocomplete", "one-time-code", "regexp", 0).IsVoid) Thread.Sleep(1000);

            if (!_instance.ActiveTab.FindElementByAttribute("div", "innertext", "Are\\ you\\ human\\?", "regexp", 0).IsVoid)
            {
                if ((_project.Variables["humanNear"].Value) != "True") return "capcha";
                else _instance.WaitForUserAction(100, "dsCap");
            }
            _instance.HeSet(("input:text", "autocomplete", "one-time-code", "regexp", 0), OTP.Offline(_project.Variables["discord2FACODE"].Value));
            _instance.HeClick(("button", "type", "submit", "regexp", 0));
            Thread.Sleep(3000);
            return "ok";
        }
        public string DSload(bool log = false)
        {

            _sql.Discord();

            string state = null;
            var emu = _instance.UseFullMouseEmulation;
            _instance.UseFullMouseEmulation = false;
            bool tokenUsed = false;
            _instance.ActiveTab.Navigate("https://discord.com/channels/@me", "");

        start:
            state = null;
            while (string.IsNullOrEmpty(state))
            {
                _instance.HeClick(("button", "innertext", "Continue\\ in\\ Browser", "regexp", 0), thr0w: false);
                if (!_instance.ActiveTab.FindElementByAttribute("input:text", "aria-label", "Email or Phone Number", "text", 0).IsVoid) state = "login";
                if (!_instance.ActiveTab.FindElementByAttribute("section", "aria-label", "User\\ area", "regexp", 0).IsVoid) state = "logged";
            }

            Log( state);


            if (state == "login" && !tokenUsed)
            {
                DSsetToken();
                tokenUsed = true;
                //Thread.Sleep(5000);					
                goto start;
            }

            else if (state == "login" && tokenUsed)
            {
                var login = DSlogin();
                if (login == "ok")
                {
                    Thread.Sleep(5000);
                    goto start;
                }
                else if (login == "capcha")
                    Log( "!W capcha");
                _instance.UseFullMouseEmulation = emu;
                state = "capcha";
            }

            else if (state == "logged")
            {
                state = _instance.ActiveTab.FindElementByAttribute("div", "class", "avatarWrapper__", "regexp", 0).FirstChild.GetAttribute("aria-label");
                
                Log( state);
                var token = DSgetToken();
                _sql.Upd($"token = '{token}', status = 'ok'", "discord");
               // DSupdateDb($"token = '{token}', status = 'ok'");
                _instance.UseFullMouseEmulation = emu;
            }
            return state;

        }
        public string DSservers()
        {
            _instance.UseFullMouseEmulation = true;
            var folders = new List<HtmlElement>();
            var servers = new List<string>();
            var list = _instance.ActiveTab.FindElementByAttribute("div", "aria-label", "Servers", "regexp", 0).GetChildren(false).ToList();
            foreach (HtmlElement item in list)
            {

                if (item.GetAttribute("class").Contains("listItem"))
                {
                    var server = item.FindChildByTag("div", 1).FirstChild.GetAttribute("data-dnd-name");
                    servers.Add(server);
                }

                if (item.GetAttribute("class").Contains("wrapper"))
                {
                    _instance.HeClick(item);
                    var FolderServer = item.FindChildByTag("ul", 0).GetChildren(false).ToList();
                    //_project.SendInfoToLog(FolderServer.Count.ToString());
                    foreach (HtmlElement itemInFolder in FolderServer)
                    {
                        var server = itemInFolder.FindChildByTag("div", 1).FirstChild.GetAttribute("data-dnd-name");
                        servers.Add(server);
                    }
                }

            }

            string result = string.Join(" | ", servers);
            _sql.Upd($"servers = '{result}'", "discord");
            //DSupdateDb($"servers = '{result}'");
            //_project.SendInfoToLog(servers.Count.ToString());
            //_project.SendInfoToLog(string.Join(" | ",servers));
            return result;
        }
    }

}
