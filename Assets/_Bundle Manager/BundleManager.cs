﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

public class BundleManager : MonoBehaviour 
{
	public static BundleManager instance;

	[SerializeField] string pathToBundles;

	Dictionary<string, AssetBundle> bundles;
	Dictionary<string, string> bundleVariants;
	AssetBundleManifest manifest = null;
	string platform;

	public bool isReady 
	{
		get { return !object.ReferenceEquals(manifest, null);}
	}


	void Awake()
	{
		System.Environment.SetEnvironmentVariable("MONO_REFLECTION_SERIALIZER","yes");

		if (object.ReferenceEquals (instance, null)) 
		{
			instance = this;
		}
		else if (!object.ReferenceEquals (instance, this))
		{
			Destroy (gameObject);
			return;
		}

		DontDestroyOnLoad (gameObject);

		platform = "";

		#if UNITY_IOS
				platform = "iOS";
		#elif UNITY_ANDROID
				platform = "Android";
		#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
				platform = "PC";
		#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
				platform = "OSX";
		#elif UNITY_WEBPLAYER
				platform = "Web";
		#elif UNITY_WP8
				platform = "WP8";
		#else
				platform = "error";
				Debug.Log("unsupported platform");
		#endif

		pathToBundles += platform + "/";
		bundles = new Dictionary<string, AssetBundle> ();
		bundleVariants = new Dictionary<string, string> ();
		StartCoroutine (LoadManifest());
	}

	IEnumerator LoadManifest () 
	{
		Debug.Log( "Loading Manifest");

		AssetBundle bundle;

		using(WWW www = new WWW(pathToBundles + platform))
		{
			yield return www;
			if(!string.IsNullOrEmpty(www.error))
			{
				Debug.Log(www.error);
				Debug.Log("Attempting to load a local copy of the manifest");

				bundle = LoadManifestFromFile();
				if(object.ReferenceEquals(bundle, null))
				{
					Debug.Log ("No local copy of manifest bundle found");
					yield break;
				}
			}
			else
			{
				SaveManifestToFile(www);
			}

			manifest = (AssetBundleManifest)www.assetBundle.LoadAsset("AssetBundleManifest", typeof(AssetBundleManifest));
			yield return null;
			www.assetBundle.Unload(false);
		} 

		if (!isReady)
			Debug.Log ("There was an error loading manifest");
		else 
			Debug.Log ("Manifest loaded successfully");
	}

	void SaveManifestToFile(WWW www)
	{
		File.WriteAllBytes (Application.persistentDataPath + "/" + platform, www.bytes);
	}

	AssetBundle LoadManifestFromFile()
	{
		if (!File.Exists (Application.persistentDataPath + "/" + platform))
		{
			Debug.Log("Local copy of manifest doesn't exist");
			return null;
		}

		try
		{
			byte[]  bytes = File.ReadAllBytes (Application.persistentDataPath + "/" + platform);
			AssetBundle bundle;

			using (MemoryStream mem = new MemoryStream())
			{
				BinaryFormatter bf = new BinaryFormatter();
				mem.Write(bytes, 0, bytes.Length);
				mem.Seek(0, SeekOrigin.Begin);
				using (WWW www = (WWW)bf.Deserialize(mem))
				{
					bundle = www.assetBundle;
				}
				return bundle;
			}
		}
		catch(System.Exception e)
		{
			Debug.Log(e.Message);
			return null;
		}
	}

	public bool IsBundleLoaded(string bundleName)
	{
		return bundles.ContainsKey (bundleName);
	}

	public void RegisterVariant(string bundleName, string variantName)
	{
		if (bundleVariants.ContainsValue (bundleName)) 
		{
			Debug.Log(string.Format("Variant for {0} cannot be added. {1} already registered. " +
				"Two vartiants of same bundle cannot be loaded (this is a safety check)", bundleName, variantName));
			return;
		}

		bundleVariants.Add (bundleName, variantName);
	}

	public Object GetAssetFromBundle(string bundleName, string assetName)
	{
		if (!IsBundleLoaded (bundleName))
			return null;

		return bundles [bundleName].LoadAsset (assetName);
	}

	public bool LoadBundle(string bundleName)
	{
		if (IsBundleLoaded(bundleName))
			return true;

		StartCoroutine(LoadBundleCoroutine(bundleName));
		return false;
	}

	IEnumerator LoadBundleCoroutine(string bundleName)
	{
		if (IsBundleLoaded (bundleName))
			yield break;

		string[] dependencies = manifest.GetAllDependencies (bundleName);
		for (int i = 0; i < dependencies.Length; i++)
			yield return StartCoroutine (LoadBundleCoroutine (dependencies [i]));

		bundleName = RemapVariantName (bundleName);
		string url = pathToBundles + bundleName;
		Debug.Log ("Beginning to load " + bundleName + " / " + manifest.GetAssetBundleHash(bundleName));

		using(WWW www = WWW.LoadFromCacheOrDownload(url, manifest.GetAssetBundleHash(bundleName)))
		{
			yield return www;
			if(!string.IsNullOrEmpty(www.error))
			{
				Debug.Log(www.error);
				yield break;
			}

			bundles.Add(bundleName, www.assetBundle);
		}
		Debug.Log ("Finished loading " + bundleName);
	}

	void OnDisable()
	{
		Debug.Log ("Unloading Bundles");

		foreach(KeyValuePair<string, AssetBundle> entry in bundles)
			entry.Value.Unload(false);
		bundles.Clear ();

		Debug.Log ("Bundles Unloaded");
	}
	
	string RemapVariantName(string assetBundleName)
	{
		string[] splitBundleName = assetBundleName.Split('.');
		string variant;

		if (!bundleVariants.TryGetValue(splitBundleName[0], out variant))
			return assetBundleName;

		string[] bundlesWithVariant = manifest.GetAllAssetBundlesWithVariant();
		string newBundleName = splitBundleName [0] + "." + variant;

		if (System.Array.IndexOf(bundlesWithVariant, newBundleName) < 0 )
			return assetBundleName;

		return newBundleName;
	}
}




