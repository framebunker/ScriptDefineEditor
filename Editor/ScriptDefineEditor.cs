using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;


namespace framebunker
{
	static class StringExtensions
	{
		public static int ParseDefine (this string line, string prefix, Action<string> onFound)
		{
			int start = line.IndexOf (prefix, StringComparison.InvariantCultureIgnoreCase);

			if (start < 0)
			{
				return 0;
			}

			return line.Substring (start + prefix.Length).ParseDefineNames (onFound);
		}


		static int ParseDefineNames (this string line, Action<string> onFound)
		{
			string[] elements = line.Split (new string[] {"||", "&&"}, StringSplitOptions.RemoveEmptyEntries);

			int count = 0;
			foreach (string name in elements)
			{
				string final = name.ParseDefineName ();

				if (!string.IsNullOrEmpty (final))
				{
					++count;
					onFound (final);
				}
			}

			return count;
		}


		static string ParseDefineName (this string name)
		{
			name = name.Trim ('!', '(', ')', ' ', '\t', '\n', '\r');
			Match match = Regex.Match (name, "[^a-zA-Z0-9_]");

			return !match.Success ? name : name.Substring (0, match.Index);
		}
	}


	public class ScriptDefineEditor : EditorWindow
	{
		const string
			kMenuPath = "Window/Script Define Editor",

			kDefinesCacheName = "CachedPreprocessorDefines",
			kIgnoredPathsName = "PreprocessorDefinesIgnoredPaths",
			kDefaultIgnoredPaths = "WebPlayerTemplates/";
		readonly string[]
			kIgnoredDefines = new[]
			{
				"true",
				"false",
				"DEBUG",
				"DEVELOPMENT_BUILD",
				"ENABLE_MONO",
				"ENABLE_IL2CPP",
				"ENABLE_DOTNET",
				"NETFX_CORE",
				"NET_2_0",
				"NET_2_0_SUBSET",
				"NET_4_6",
				"ENABLE_WINMD_SUPPORT"
			};


		static bool s_Applying = false;
		static GUIStyle
			s_BoldToggle,
			s_ToolbarLabel;
		static string s_NewIgnoredPath = "";


		static GUIStyle BoldToggleStyle
		{
			get
			{
				return s_BoldToggle = s_BoldToggle ?? new GUIStyle (GUI.skin.toggle)
				{
					name = "Bold Toggle",
					fontStyle = FontStyle.Bold
				};
			}
		}


		static GUIStyle ToolbarLabel
		{
			get
			{
				return s_ToolbarLabel = s_ToolbarLabel ?? new GUIStyle (EditorStyles.miniLabel)
				{
					name = "Toolbar Label",
					margin = new RectOffset (0, 0, 4, 4),
					padding = new RectOffset()
				};
			}
		}


		static string s_EnabledDefinesStringCache = "";
		readonly static List<string> s_EnabledDefinesCache = new List<string> ();
		static IEnumerable<string> EnabledDefines
		{
			get
			{
				string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup (TargetToGroup (EditorUserBuildSettings.activeBuildTarget));

				if (currentDefines != s_EnabledDefinesStringCache)
				{
					s_EnabledDefinesCache.Clear ();
					s_EnabledDefinesCache.AddRange (currentDefines.Split (';'));
					s_EnabledDefinesStringCache = currentDefines;
				}

				return s_EnabledDefinesCache;
			}
			set
			{
				s_EnabledDefinesCache.Clear ();
				s_EnabledDefinesCache.AddRange (value);

				string newDefines = s_EnabledDefinesCache.Aggregate ((all, current) => string.Format ("{0};{1}", all, current));

				if (newDefines == s_EnabledDefinesStringCache)
				{
					return;
				}

				PlayerSettings.SetScriptingDefineSymbolsForGroup (TargetToGroup (EditorUserBuildSettings.activeBuildTarget), newDefines);
			}
		}


		static List<string> s_CachedDefines;
		static IEnumerable<string> CachedDefines
		{
			get
			{
				return s_CachedDefines = s_CachedDefines ?? new List<string> (EditorPrefs.GetString (kDefinesCacheName, "").Split ('\n'));
			}
			set
			{
				s_CachedDefines = value == null ? null : new List<string> (value);

				if (s_CachedDefines == null || s_CachedDefines.Count < 1)
				{
					s_CachedDefines = null;
					EditorPrefs.SetString (kDefinesCacheName, "");
					return;
				}

				EditorPrefs.SetString (kDefinesCacheName, s_CachedDefines.Aggregate ((all, current) => string.Format ("{0}\n{1}", all, current)));
			}
		}


		static List<string> s_IgnoredPaths;
		static IEnumerable<string> IgnoredPaths
		{
			get
			{
				return s_IgnoredPaths = s_IgnoredPaths ?? new List<string> (EditorPrefs.GetString (kIgnoredPathsName, kDefaultIgnoredPaths).Split ('\n'));
			}
		}


		static void SaveIgnoredPaths ()
		{
			EditorPrefs.SetString (kIgnoredPathsName, IgnoredPaths.Aggregate ((all, current) => string.Format ("{0}\n{1}", all, current)));
		}


		static void IgnorePath (string path)
		{
			if (IgnoredPaths == null || s_IgnoredPaths.Contains (path))
			{
				return;
			}

			s_IgnoredPaths.Add (path);
			SaveIgnoredPaths ();
		}


		static void UnignorePath (string path)
		{
			if (IgnoredPaths == null || !s_IgnoredPaths.Contains (path))
			{
				return;
			}

			s_IgnoredPaths.Remove (path);
			SaveIgnoredPaths ();
		}


		[InitializeOnLoadMethod]
		static void OnScriptReload ()
		{
			s_Applying = false;
		}


		[MenuItem (kMenuPath)]
		static void Launch ()
		{
			GetWindow<ScriptDefineEditor> ();
		}


		static BuildTargetGroup TargetToGroup (BuildTarget target)
		{
			switch (target)
			{
				case BuildTarget.StandaloneOSXIntel:
				case BuildTarget.StandaloneOSXIntel64:
				case BuildTarget.StandaloneOSXUniversal:
				case BuildTarget.StandaloneLinux:
				case BuildTarget.StandaloneLinux64:
				case BuildTarget.StandaloneLinuxUniversal:
				case BuildTarget.StandaloneWindows:
				case BuildTarget.StandaloneWindows64:
				return BuildTargetGroup.Standalone;
				case BuildTarget.iOS:
				return BuildTargetGroup.iOS;
				case BuildTarget.Android:
				return BuildTargetGroup.Android;
				case BuildTarget.WebGL:
				return BuildTargetGroup.WebGL;
				case BuildTarget.WSAPlayer:
				return BuildTargetGroup.WSA;
				case BuildTarget.Tizen:
				return BuildTargetGroup.Tizen;
				case BuildTarget.PSP2:
				return BuildTargetGroup.PSP2;
				case BuildTarget.PS4:
				return BuildTargetGroup.PS4;
				case BuildTarget.XboxOne:
				return BuildTargetGroup.XboxOne;
				case BuildTarget.N3DS:
				return BuildTargetGroup.N3DS;
				case BuildTarget.WiiU:
				return BuildTargetGroup.WiiU;
				case BuildTarget.tvOS:
				return BuildTargetGroup.tvOS;
				case BuildTarget.Switch:
				return BuildTargetGroup.Switch;
				default:
				return BuildTargetGroup.Unknown;
			}
		}


		[PreferenceItem ("Script Defines")]
		static void OnPreferenceGUI ()
		{
			const string
				kIgnoredPathsLabel = "Ignored paths",
				kAddLabel = "Add",
				kRemoveLabel = "Remove";
			const float
				kPathWidth = 300f,
				kButtonWidth = 50f,
				kFieldPadding = -4f,
				kAddPadding = 5f;

			GUILayout.Label (kIgnoredPathsLabel, EditorStyles.boldLabel);

			string toRemove = null;

			foreach (string path in IgnoredPaths)
			{
				GUILayout.BeginHorizontal ();
					GUILayout.Label (path, EditorStyles.helpBox, GUILayout.Width (kPathWidth));
					GUILayout.Space (kFieldPadding);
					if (GUILayout.Button (kRemoveLabel, EditorStyles.helpBox, GUILayout.Width (kButtonWidth)))
					{
						toRemove = path;
					}
				GUILayout.EndHorizontal ();
			}

			if (toRemove != null)
			{
				UnignorePath (toRemove);
			}

			GUILayout.Space (kAddPadding);

			GUILayout.BeginHorizontal ();
				s_NewIgnoredPath = GUILayout.TextField (s_NewIgnoredPath, GUILayout.Width (kPathWidth));
				GUILayout.Space (kFieldPadding);
				if (GUILayout.Button (kAddLabel, EditorStyles.miniButtonRight, GUILayout.Width (kButtonWidth)))
				{
					IgnorePath (s_NewIgnoredPath);
					s_NewIgnoredPath = "";
				}
			GUILayout.EndHorizontal ();
		}


		HashSet<string> m_Defines = new HashSet<string> ();
		List<string>
			m_ToEnable = new List<string> (),
			m_ToDisable = new List<string> ();
		Vector2 m_Scroll;


		ScriptDefineEditor ()
		{
			titleContent = new GUIContent ("Script Defines");

			EditorApplication.delayCall += ReadCache;
		}


		void ReadCache ()
		{
			m_Defines.Clear ();

			foreach (string current in CachedDefines)
			{
				m_Defines.Add (current);
			}

			Repaint ();
		}


		void Refresh ()
		{
			const string kProgressLabel = "Parsing scripts for preprocessor defines";

			m_Defines.Clear ();
			CachedDefines = null;

			if (!UpdateProgress (0f, kProgressLabel))
			{
				return;
			}

			List<string> files = new List<string> (Directory.GetFiles (Application.dataPath, "*.cs", SearchOption.AllDirectories));
			files.FastRemoveAll (current => IgnoredPaths.Any (p => current.StartsWith (string.Format ("{0}/{1}", Application.dataPath, p))));

			for (int index = 0; index < files.Count; ++index)
			{
				string current = files[index];

				if (!UpdateProgress ((float)index / files.Count, kProgressLabel, current.Substring (Application.dataPath.Length)))
				{
					return;
				}

				using (StreamReader stream = File.OpenText (current))
				{
					string line;
					while ((line = stream.ReadLine ()) != null)
					{
						line.ParseDefine ("#define ", OnFoundDefine);
						line.ParseDefine ("#if ", OnFoundDefine);
					}
				}
			}

			CachedDefines = m_Defines;

			EditorUtility.ClearProgressBar ();
		}


		void OnFoundDefine (string name)
		{
			if (
				kIgnoredDefines.Contains (name) ||
				name.StartsWith ("UNITY_"))
			{
				return;
			}

			m_Defines.Add (name);
		}


		bool UpdateProgress (float progress, string label, string subLabel = "", string cancelMessage = "User canceled")
		{
			if (EditorUtility.DisplayCancelableProgressBar (label, subLabel, progress))
			{
				Debug.LogError (cancelMessage);
				EditorUtility.ClearProgressBar ();
				return false;
			}

			return true;
		}


		void OnGUI ()
		{
			const string
				kDefinesLabelFormat = "Defines ({0})",
				kRefreshLabel = "Refresh",
				kChangesLabelFormat = "Changes: {0}",
				kApplyingLabel = "Applying...",
				kClearLabel = "Clear",
				kApplyLabel = "Apply";


			GUILayout.BeginHorizontal (EditorStyles.toolbar);
				GUILayout.Label (string.Format (kDefinesLabelFormat, m_Defines.Count), ToolbarLabel);
				GUILayout.FlexibleSpace ();
				if (GUILayout.Button (kRefreshLabel, EditorStyles.toolbarButton))
				{
					EditorApplication.delayCall += Refresh;
				}
			GUILayout.EndHorizontal ();

			m_Scroll = GUILayout.BeginScrollView (m_Scroll);
				GUILayout.BeginVertical ();
					foreach (string current in m_Defines)
					{
						DefineItem (current);
					}
				GUILayout.EndVertical ();
			GUILayout.EndScrollView ();

			GUILayout.FlexibleSpace ();

			int changeCount;
			if (s_Applying)
			{
				GUILayout.BeginHorizontal (EditorStyles.toolbar);
					GUILayout.FlexibleSpace ();
					GUILayout.Label (kApplyingLabel, ToolbarLabel);
					GUILayout.FlexibleSpace ();
				GUILayout.EndHorizontal ();
			}
			else if ((changeCount = m_ToEnable.Count + m_ToDisable.Count) > 0)
			{
				GUILayout.BeginHorizontal (EditorStyles.toolbar);
					if (GUILayout.Button (kClearLabel, EditorStyles.toolbarButton))
					{
						m_ToEnable.Clear ();
						m_ToDisable.Clear ();
					}
					GUILayout.FlexibleSpace ();
					GUILayout.Label (string.Format (kChangesLabelFormat, changeCount), ToolbarLabel);
					GUILayout.FlexibleSpace ();
					if (GUILayout.Button (kApplyLabel, EditorStyles.toolbarButton))
					{
						Apply ();
					}
				GUILayout.EndHorizontal ();
			}
		}


		void DefineItem (string name)
		{
			bool wasSet = DefineIsSet (name);
			bool isSet = GUILayout.Toggle (wasSet, name, DefineIsModified (name) ? BoldToggleStyle : GUI.skin.toggle);

			if (isSet == wasSet)
			{
				return;
			}

			if (isSet)
			{
				Enable (name);
			}
			else
			{
				Disable (name);
			}
		}


		void Enable (string name)
		{
			m_ToEnable.Add (name);
			m_ToDisable.Remove (name);
		}


		void Disable (string name)
		{
			m_ToDisable.Add (name);
			m_ToEnable.Remove (name);
		}


		void Apply ()
		{
			List<string> newDefinesList = new List<string> (EnabledDefines);

			foreach (string name in m_ToDisable)
			{
				newDefinesList.Remove (name);
			}

			newDefinesList.AddRange (m_ToEnable);

			EnabledDefines = newDefinesList;

			m_ToEnable.Clear ();
			m_ToDisable.Clear ();

			s_Applying = true;

			Repaint ();
		}


		bool DefineIsSet (string name)
		{
			return !m_ToDisable.Contains (name) && (EnabledDefines.Contains (name) || m_ToEnable.Contains (name));
		}


		bool DefineIsModified (string name)
		{
			return m_ToDisable.Contains (name) || m_ToEnable.Contains (name);
		}
	}
}
