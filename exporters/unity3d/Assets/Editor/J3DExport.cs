using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEditor;

using Antlr.StringTemplate;
using Antlr.StringTemplate.Language;

public enum Extension {json, js};

public class J3DExport : ScriptableWizard
{
	public string prefix = "J3D";
	public Extension extenstion;
	public Transform[] transforms;
	
	public bool useConsole = true;
	//public ReportLevel level = ReportLevel.Warning;
	public bool generateReport = false;
	
	private List<TransformExportData> tex;
	private List<LightExportData> lgx;
	private List<CameraExportData> cmx;
	
	private Hashtable mex;
	private Hashtable mtx;
	private Hashtable txx;
	
	//private LightmapExportData led;
	
	void OnWizardUpdate ()
	{
		if (transforms == null && Selection.transforms.Length > 0) {
			transforms = Selection.transforms;
		}	
	}
	
	void OnWizardCreate ()
	{
		// Rename all gameobjects so that there are none with the same name
		
		Report.level = ReportLevel.All;
		Report.useConsole = useConsole;
		TransformExportData.uidc = 1;
		
		Report.log ("Exporting scene: " + EditorApplication.currentScene);
		Report.log ("Platform: " + SystemInfo.operatingSystem);
		Report.log ("Unity player: " + Application.unityVersion);
		
		string meshesPath = EditorUtility.SaveFilePanel ("Save meshes", FileExport.lastExportPath, "", extenstion.ToString ());
		int extl = (extenstion == Extension.js) ? 3 : 5;
		string scenePath = meshesPath.Substring (0, meshesPath.Length - extl) + "Scene." + extenstion.ToString ();
		string texturePath = meshesPath.Substring (0, meshesPath.LastIndexOf ("/") + 1);
		string reportPath = meshesPath.Substring (0, meshesPath.Length - extl) + "Log.txt";
		
		Report.log ("Exporting to: " + meshesPath + " " + scenePath);

		//led = new LightmapExportData ();
		
		tex = new List<TransformExportData> ();
		lgx = new List<LightExportData> ();
		cmx = new List<CameraExportData> ();
		
		mex = new Hashtable ();
		mtx = new Hashtable ();
		txx = new Hashtable ();

		for (var i = 0; i < transforms.Length; i++) {
			RecurseTransform (transforms[i], null);
		}
		
		// Some textures might have been marked as readable for exporting, apply those changes now.
		AssetDatabase.Refresh ();

		StringTemplate mt = FileExport.LoadTemplate ("model");
		mt.SetAttribute ("prefix", prefix + "Meshes");
		mt.SetAttribute ("meshes", mex.Values);
		FileExport.SaveContentsAsFile (FileExport.CleanJSON (mt), meshesPath);
		
		StringTemplate st = FileExport.LoadTemplate ("scene");
		st.SetAttribute ("ambient", RenderSettings.ambientLight);
		
		if (Camera.mainCamera != null)
			st.SetAttribute ("background", Camera.mainCamera.backgroundColor);
		else
			st.SetAttribute ("background", Color.black);
		
		st.SetAttribute ("prefix", prefix + "Scene");
		st.SetAttribute ("meshPrefix", prefix + "Meshes");
		st.SetAttribute ("transforms", tex);
		st.SetAttribute ("root", tex[0]);
		st.SetAttribute ("meshes", mex.Values);
		st.SetAttribute ("materials", mtx.Values);
		st.SetAttribute ("textures", txx.Values);
		st.SetAttribute ("lights", lgx);
		st.SetAttribute ("cameras", cmx);
		FileExport.SaveContentsAsFile (FileExport.CleanJSON (st), scenePath);
		
		foreach (TextureExportData t in txx.Values) {
			if (t.IsImage) {
				if (t.Format != TextureImporterFormat.ARGB32 && t.Format != TextureImporterFormat.RGB24) {
					Report.error ("Texture not exported. '" + t.Name + "' has wrong format: " + t.Format.ToString () + ", should be ARGB32 or RGB24");
				} else if (!t.asset.isReadable) {
					Report.error ("Texture not exported. '" + t.Name + "' not marked as readable.");
				} else {
					File.WriteAllBytes (texturePath + t.FileName, t.pngData);
					Report.log ("Exporting texture " + t.Name + " to " + texturePath + t.FileName);
				}
			}
		}
		
		Report.log ("Exported " + tex.Count + " transforms");

		FileExport.lastExportPath = meshesPath;
		
		Report.log ("Done!");
		if(generateReport) Report.saveReport (reportPath);
	}
	
	void RecurseTransform (Transform t, TransformExportData p)
	{
		if (!t.gameObject.active)
			return;
		
		TransformExportData ted = new TransformExportData (t, p);
		
		tex.Add (ted);
		
		if (t.renderer != null) {
			MeshExportData me = new MeshExportData (t);
			if (!mex.ContainsKey (me.Name))
				mex.Add (me.Name, me);
			
			List<string> textures = TextureUtil.ExtractTexturesNames (t.renderer.sharedMaterial);
			
			MaterialExportData mt = new MaterialExportData (t, textures);
			if (!mtx.ContainsKey (mt.Name))
				mtx.Add (mt.Name, mt);
			
			foreach (string tn in textures) {
				TextureExportData tx = new TextureExportData (t.renderer.sharedMaterial.GetTexture (tn));
				if (!txx.ContainsKey (tx.Name))
					txx.Add (tx.Name, tx);
			}
			
		}
		
		if (t.light != null) {
			LightExportData ld = new LightExportData (t);
			lgx.Add (ld);
		}
		
		if (t.camera != null) {
			CameraExportData cd = new CameraExportData (t);
			cmx.Add (cd);
		}
		
		for (var i = 0; i < t.childCount; i++) {
			RecurseTransform (t.GetChild (i), ted);
		}
	}

	[MenuItem("J3D/Export")]
	static void Export ()
	{
		ScriptableWizard.DisplayWizard ("Export to J3D", typeof(J3DExport), "Export");
	}
}
