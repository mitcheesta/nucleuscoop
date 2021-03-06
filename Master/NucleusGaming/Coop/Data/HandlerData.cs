﻿using Jint;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;
using Nucleus.Gaming.Coop;
using Jint.Runtime.Interop;
using Nucleus.Gaming.Coop.Handler;

namespace Nucleus.Gaming.Coop
{
    public class HandlerData
    {
        private Engine engine;
        private string jsCode;
        
        public GameHookData Hook { get; set; } = new GameHookData();
        public List<GameOption> Options { get; set; } = new List<GameOption>();

        public SaveType SaveType { get; set; }
        public string SavePath { get; set; }

        public string[] DirSymlinkExclusions { get; set; }
        public string[] FileSymlinkExclusions { get; set; }
        public string[] FileSymlinkCopyInstead { get; set; }

        public bool ForceFinishOnPlay { get; set; } = true;
        public double HandlerInterval { get; set; }
        public bool Debug { get; set; }
        public bool SupportsPositionin { get; set; }
        public bool SymlinkExe { get; set; }
        public bool SymlinkGame { get; set; }
        public bool HardcopyGame { get; set; }

        public bool SupportsKeyboard { get; set; }
        public string[] ExecutableContext { get; set; }
        public string ExecutableName { get; set; }
        public string SteamID { get; set; }
        public string GUID { get; set; }
        public string GameName { get; set; }
        public int MaxPlayers { get; set; }
        public int MaxPlayersOneMonitor { get; set; }
        public int PauseBetweenStarts { get; set; }
        public DPIHandling DPIHandling { get; set; } = DPIHandling.True;

        public string StartArguments { get; set; }
        public string BinariesFolder { get; set; }

        /// <summary>
        /// The relative path to where the games starts in
        /// </summary>
        public string WorkingFolder { get; set; }
        public bool NeedsSteamEmulation { get; set; }
        public string[] KillMutex { get; set; }
        public string LauncherExe { get; set; }
        public string LauncherTitle { get; set; }
        public Action Play { get; set; }
        public Action SetupSse { get; set; }
        public List<CustomStep> CustomSteps { get; set; } = new List<CustomStep>();
        public bool LockMouse { get; set; }

        public Dictionary<string, string> AdditionalData { get; set; } = new Dictionary<string, string>();

        public Type HandlerType
        {
            get { return typeof(GenericGameHandler); }
        }

        public HandlerData(Stream str)
        {
            StreamReader reader = new StreamReader(str);
            jsCode = reader.ReadToEnd();

            ParseJs(jsCode);
        }

        private bool executing;

        public HandlerData(string jsCode)
        {
            ParseJs(jsCode);
        }

        private void ParseJs(string jsCode)
        {
            // get the Nucleus.Gaming assembly
            AdditionalData = new Dictionary<string, string>();

            engine = new Engine();

            engine.SetValue("SaveType", TypeReference.CreateTypeReference(engine, typeof(SaveType)));
            engine.SetValue("DPIHandling", TypeReference.CreateTypeReference(engine, typeof(DPIHandling)));
            engine.SetValue("Folder", TypeReference.CreateTypeReference(engine, typeof(Folder)));
            engine.SetValue("SaveType", TypeReference.CreateTypeReference(engine, typeof(SaveType)));

            engine.SetValue("Game", this);
            engine.Execute(jsCode);
        }

        //private void Execute()
        //{
        //    executing = true;
        //    engine.Execute(jsCode);
        //    executing = false;
        //}

        public void RegisterAdditional(string key, string value)
        {
            //if (executing)
            //{
            //    return;
            //}

            if (!AdditionalData.ContainsKey(key))
            {
                AdditionalData.Add(key, value);
            }
            else
            {
                AdditionalData[key] = value;
            }
        }

        public CustomStep ShowOptionAsStep(string optionKey, bool required, string title)
        {
            GameOption option = Options.First(c => c.Key == optionKey);
            option.Hidden = true;

            CustomStep step = new CustomStep();
            step.Option = option;
            step.Required = required;
            step.Title = title;

            CustomSteps.Add(step);
            return step;
        }

        public void PrePlay(HandlerContext context, PlayerInfo player)
        {
            engine.SetValue("Context", context);
            engine.SetValue("Player", player);
            engine.SetValue("Game", this);

            Play?.Invoke();
        }

        /// <summary>
        /// Clones this Game Info into a new Generic Context
        /// </summary>
        /// <returns></returns>
        public HandlerContext CreateContext(GameProfile profile, PlayerInfo info)
        {
            HandlerContext context = new HandlerContext(profile, info);

            Type t = GetType();
            PropertyInfo[] props = t.GetProperties();
            FieldInfo[] fields = t.GetFields();

            Type c = context.GetType();
            PropertyInfo[] cprops = c.GetProperties();
            FieldInfo[] cfields = c.GetFields();

            for (int i = 0; i < props.Length; i++)
            {
                PropertyInfo source = props[i];
                PropertyInfo dest = cprops.FirstOrDefault(k => k.Name == source.Name);

                if (dest != null && 
                    source.PropertyType == dest.PropertyType && 
                    dest.CanWrite)
                {
                    // TODO: this is dangerous for lists/dictionaries if the handler changes the size of anything
                    object value = source.GetValue(this, null);
                    dest.SetValue(context, value, null);
                }
                else
                {
                    FieldInfo fdest = cfields.FirstOrDefault(k => k.Name == source.Name);
                    if (fdest != null &&
                        source.PropertyType == fdest.FieldType)
                    {
                        object value = source.GetValue(this, null);
                        fdest.SetValue(context, value);
                    }
                }
            }

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo source = fields[i];
                FieldInfo dest = cfields.FirstOrDefault(k => k.Name == source.Name);
                if (dest == null ||
                    source.FieldType != dest.FieldType)
                {
                    continue;
                }

                object value = source.GetValue(this);
                dest.SetValue(context, value);
            }

            return context;
        }

        public string GetSteamLanguage()
        {
            string result;
            if (Environment.Is64BitOperatingSystem)
            {
                result = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "Language", null);
            }
            else
            {
                result = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "Language", null);
            }

            return result;
        }

        public void AddOption(string name, string desc, string key, object value, object defaultValue)
        {
            Options.Add(new GameOption(name, desc, key, value, defaultValue));
        }

        public void AddOption(string name, string desc, string key, object value)
        {
            Options.Add(new GameOption(name, desc, key, value));
        }
    }
}
