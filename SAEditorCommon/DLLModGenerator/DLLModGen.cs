﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SA_Tools;

namespace SonicRetro.SAModel.SAEditorCommon.DLLModGenerator
{
	public static class DLLModGen
	{
		static readonly Dictionary<string, string> typemap = new Dictionary<string, string>() {
			{ "landtable", "LandTable *" },
			{ "landtablearray", "LandTable **" },
			{ "model", "NJS_OBJECT *" },
			{ "modelarray", "NJS_OBJECT **" },
			{ "basicmodel", "NJS_OBJECT *" },
			{ "basicmodelarray", "NJS_OBJECT **" },
			{ "basicdxmodel", "NJS_OBJECT *" },
			{ "basicdxmodelarray", "NJS_OBJECT **" },
			{ "chunkmodel", "NJS_OBJECT *" },
			{ "chunkmodelarray", "NJS_OBJECT **" },
			{ "actionarray", "NJS_ACTION **" },
			{ "morph", "NJS_MODEL_SADX *" },
			{ "modelsarray", "NJS_MODEL_SADX **" },
			{ "texlist", "NJS_TEXLIST *" },
			{ "texlistarray", "NJS_TEXLIST **" },
			{ "animindexlist", "AnimationIndex *" }
		};

		public static DllIniData LoadINI(string fileName,
			ref Dictionary<string, bool> defaultExportState)
		{
			defaultExportState.Clear();

			DllIniData IniData = IniSerializer.Deserialize<DllIniData>(fileName);

			Environment.CurrentDirectory = Path.GetDirectoryName(fileName);

			foreach (KeyValuePair<string, FileTypeHash> item in IniData.Files)
			{
				bool modified = HelperFunctions.FileHash(item.Key) != item.Value.Hash;
				defaultExportState.Add(item.Key, modified);
			}

			return IniData;
		}

		public static void ExportINI(DllIniData IniData,
			Dictionary<string, bool> itemsToExport, string fileName)
		{
			string dstfol = Path.GetDirectoryName(fileName);
			DllIniData output = new DllIniData()
			{
				Name = IniData.Name,
				Game = IniData.Game,
				Exports = IniData.Exports,
				TexLists = IniData.TexLists,
				Files = new DictionaryContainer<FileTypeHash>()
			};
			List<string> labels = new List<string>();
			foreach (KeyValuePair<string, FileTypeHash> item in
				IniData.Files.Where(i => itemsToExport[i.Key]))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dstfol, item.Key)));
				File.Copy(item.Key, Path.Combine(dstfol, item.Key), true);
				switch (item.Value.Type)
				{
					case "landtable":
						LandTable tbl = LandTable.LoadFromFile(item.Key);
						labels.AddRange(tbl.GetLabels());
						break;
					case "model":
					case "basicmodel":
					case "chunkmodel":
					case "basicdxmodel":
						NJS_OBJECT mdl = new ModelFile(item.Key).Model;
						labels.AddRange(mdl.GetLabels());
						break;
					case "animation":
					case "animindex":
						NJS_MOTION ani = NJS_MOTION.Load(item.Key);
						labels.Add(ani.Name);
						break;
				}
				output.Files.Add(item.Key, new FileTypeHash(item.Value.Type, null));
			}
			output.Items = new List<DllItemInfo>(IniData.Items.Where(a => labels.Contains(a.Label)));
			IniSerializer.Serialize(output, fileName);
		}

		public static void ExportCPP(DllIniData IniData,
			Dictionary<string, bool> itemsToExport, string fileName)
		{
			using (TextWriter writer = File.CreateText(fileName))
			{
				bool SA2 = IniData.Game == Game.SA2B;
				ModelFormat modelfmt = SA2 ? ModelFormat.Chunk : ModelFormat.BasicDX;
				LandTableFormat landfmt = SA2 ? LandTableFormat.SA2 : LandTableFormat.SADX;
				writer.WriteLine("// Generated by SA Tools DLL Mod Generator");
				writer.WriteLine();
				if (SA2)
					writer.WriteLine("#include \"SA2ModLoader.h\"");
				else
					writer.WriteLine("#include \"SADXModLoader.h\"");
				writer.WriteLine();
				List<string> labels = new List<string>();
				Dictionary<string, uint> texlists = new Dictionary<string, uint>();
				foreach (KeyValuePair<string, FileTypeHash> item in
					IniData.Files.Where(i => itemsToExport[i.Key]))
				{
					switch (item.Value.Type)
					{
						case "landtable":
							LandTable tbl = LandTable.LoadFromFile(item.Key);
							texlists.Add(tbl.Name, tbl.TextureList);
							tbl.ToStructVariables(writer, landfmt, new List<string>());
							labels.AddRange(tbl.GetLabels());
							break;
						case "model":
							NJS_OBJECT mdl = new ModelFile(item.Key).Model;
							mdl.ToStructVariables(writer, modelfmt == ModelFormat.BasicDX, new List<string>());
							labels.AddRange(mdl.GetLabels());
							break;
						case "basicmodel":
						case "chunkmodel":
							mdl = new ModelFile(item.Key).Model;
							mdl.ToStructVariables(writer, false, new List<string>());
							labels.AddRange(mdl.GetLabels());
							break;
						case "basicdxmodel":
							mdl = new ModelFile(item.Key).Model;
							mdl.ToStructVariables(writer, true, new List<string>());
							labels.AddRange(mdl.GetLabels());
							break;
						case "animation":
						case "animindex":
							NJS_MOTION ani = NJS_MOTION.Load(item.Key);
							ani.ToStructVariables(writer);
							labels.Add(ani.Name);
							break;
					}
					writer.WriteLine();
				}
				writer.WriteLine("extern \"C\" __declspec(dllexport) void __cdecl Init(const char *path, const HelperFunctions &helperFunctions)");
				writer.WriteLine("{");
				writer.WriteLine("\tHMODULE handle = GetModuleHandle(L\"{0}\");", IniData.Name);
				List<string> exports = new List<string>(IniData.Items.Where(item => labels.Contains(item.Label)).Select(item => item.Export).Distinct());
				foreach (KeyValuePair<string, string> item in IniData.Exports.Where(item => exports.Contains(item.Key)))
					writer.WriteLine("\t{0}{1} = ({0})GetProcAddress(handle, \"{1}\");", typemap[item.Value], item.Key);
				foreach (DllItemInfo item in IniData.Items.Where(item => labels.Contains(item.Label)))
					writer.WriteLine("\t{0} = &{1};", item.ToString(), item.Label);
				if (texlists.Count > 0 && IniData.TexLists != null && IniData.TexLists.Items != null)
				{
					exports = new List<string>(IniData.TexLists.Where(item => texlists.Values.Contains(item.Key)).Select(item => item.Value.Export).Distinct());
					foreach (KeyValuePair<string, string> item in IniData.Exports.Where(item => exports.Contains(item.Key)))
						writer.WriteLine("\t{0}{1} = ({0})GetProcAddress(handle, \"{1}\");", typemap[item.Value], item.Key);
					foreach (KeyValuePair<string, uint> item in texlists.Where(item => IniData.TexLists.ContainsKey(item.Value)))
					{
						DllTexListInfo tex = IniData.TexLists[item.Value];
						string str;
						if (tex.Index.HasValue)
							str = $"{tex.Export}[{tex.Index.Value}]";
						else
							str = tex.Export;
						writer.WriteLine("\t{0}.TexList = {1};", item.Key, str);
					}
				}
				writer.WriteLine("}");
				writer.WriteLine();
				writer.WriteLine("extern \"C\" __declspec(dllexport) const ModInfo {0}ModInfo = {{ ModLoaderVer }};", SA2 ? "SA2" : "SADX");
			}
		}
	}
}
