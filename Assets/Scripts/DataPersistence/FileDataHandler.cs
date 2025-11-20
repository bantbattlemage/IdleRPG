using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

public class FileDataHandler
{
	private string dataDirPath = "";
	private string dataFileName = "";
	private bool useEncryption = false;
	private readonly string encryptionCodeWord = "word"; // consider moving to secure storage
	private readonly string backupExtension = ".bak";

	public FileDataHandler(string dataDirPath, string dataFileName, bool useEncryption)
	{
		this.dataDirPath = dataDirPath;
		this.dataFileName = dataFileName;
		this.useEncryption = useEncryption;
	}

	public GameData Load(string profileId, bool allowRestoreFromBackup = true)
	{
		// base case - if the profileId is null, return right away
		if (profileId == null)
		{
			return null;
		}

		string fullPath = Path.Combine(dataDirPath, profileId, dataFileName);
		GameData loadedData = null;

		if (!File.Exists(fullPath))
		{
			return null;
		}

		int attempts = 0;
		bool triedRollback = false;

		while (attempts < 2)
		{
			attempts++;
			try
			{
				// read the file
				string dataToLoad;
				if (useEncryption)
				{
					byte[] fileBytes = File.ReadAllBytes(fullPath);
					dataToLoad = DecryptBytes(fileBytes);
				}
				else
				{
					dataToLoad = File.ReadAllText(fullPath);
				}

				// deserialize
				loadedData = JsonUtility.FromJson<GameData>(dataToLoad);
				// success
				return loadedData;
			}
			catch (Exception e)
			{
				Debug.LogWarning($"Failed to load data file at {fullPath}: {e}");

				if (allowRestoreFromBackup && !triedRollback)
				{
					triedRollback = true;
					bool rollbackSuccess = AttemptRollback(fullPath);
					if (!rollbackSuccess)
					{
						// no backup or rollback failed; break
						Debug.LogError("Rollback failed or no backup available.");
						break;
					}
					// if rollback succeeded, loop will retry once
				}
				else
				{
					// either not allowed to rollback or already tried; stop
					break;
				}
			}
		}

		return null;
	}

	public void Save(GameData data, string profileId)
	{
		// base case - if the profileId is null, return right away
		if (profileId == null)
		{
			return;
		}

		string profileDir = Path.Combine(dataDirPath, profileId);
		string fullPath = Path.Combine(profileDir, dataFileName);
		string backupFilePath = fullPath + backupExtension;
		string tempFilePath = fullPath + ".tmp";

		try
		{
			// create the directory the file will be written to if it doesn't already exist
			Directory.CreateDirectory(profileDir);

			// serialize the C# game data object into Json
			string dataToStore = JsonUtility.ToJson(data, true);

			// optionally encrypt the data and write to a temp file
			if (useEncryption)
			{
				byte[] encrypted = EncryptToBytes(dataToStore);
				File.WriteAllBytes(tempFilePath, encrypted);
			}
			else
			{
				File.WriteAllText(tempFilePath, dataToStore, Encoding.UTF8);
			}

			// verify the newly saved temp file can be loaded successfully (without invoking rollback recursion)
			GameData verifiedGameData = null;
			try
			{
				if (useEncryption)
				{
					byte[] tempBytes = File.ReadAllBytes(tempFilePath);
					string decrypted = DecryptBytes(tempBytes);
					verifiedGameData = JsonUtility.FromJson<GameData>(decrypted);
				}
				else
				{
					string tempText = File.ReadAllText(tempFilePath);
					verifiedGameData = JsonUtility.FromJson<GameData>(tempText);
				}
			}
			catch (Exception e)
			{
				// verification failed - remove temp and throw
				File.Delete(tempFilePath);
				throw new Exception("Saved temp file could not be verified.", e);
			}

			// if the data can be verified, move it into place and create/update backup
			if (verifiedGameData != null)
			{
				if (File.Exists(fullPath))
				{
					// replace existing file and create/update backup atomically where supported
					try
					{
						// If a backup exists already, File.Replace will overwrite it. If not, provide backup path.
						File.Replace(tempFilePath, fullPath, backupFilePath, true);
					}
					catch (PlatformNotSupportedException)
					{
						// fallback: overwrite with move and create backup manually
						if (File.Exists(backupFilePath)) File.Delete(backupFilePath);
						File.Copy(fullPath, backupFilePath, true);
						File.Delete(fullPath);
						File.Move(tempFilePath, fullPath);
					}
				}
				else
				{
					// no existing file - move temp to final location and create a backup copy
					File.Move(tempFilePath, fullPath);
					try
					{
						File.Copy(fullPath, backupFilePath, true);
					}
					catch (Exception) { /* non-fatal if backup can't be created */ }
				}
			}
			else
			{
				// verification failed
				File.Delete(tempFilePath);
				throw new Exception("Save file could not be verified and backup could not be created.");
			}

		}
		catch (Exception e)
		{
			Debug.LogError("Error occured when trying to save data to file: " + fullPath + "\n" + e);
		}
		finally
		{
			// ensure temp file is cleaned up
			if (File.Exists(tempFilePath))
			{
				try { File.Delete(tempFilePath); } catch { }
			}
		}
	}

	public void Delete(string profileId)
	{
		// base case - if the profileId is null, return right away
		if (profileId == null)
		{
			return;
		}

		string fullPath = Path.Combine(dataDirPath, profileId, dataFileName);
		try
		{
			// ensure the data file exists at this path before deleting the directory
			if (File.Exists(fullPath))
			{
				// delete the profile folder and everything within it
				Directory.Delete(Path.GetDirectoryName(fullPath), true);
			}
			else
			{
				Debug.LogWarning("Tried to delete profile data, but data was not found at path: " + fullPath);
			}
		}
		catch (Exception e)
		{
			Debug.LogError("Failed to delete profile data for profileId: "
				+ profileId + " at path: " + fullPath + "\n" + e);
		}
	}

	public Dictionary<string, GameData> LoadAllProfiles()
	{
		Dictionary<string, GameData> profileDictionary = new Dictionary<string, GameData>();

		if (!Directory.Exists(dataDirPath)) return profileDictionary;

		// loop over all directory names in the data directory path
		IEnumerable<DirectoryInfo> dirInfos = new DirectoryInfo(dataDirPath).EnumerateDirectories();
		foreach (DirectoryInfo dirInfo in dirInfos)
		{
			string profileId = dirInfo.Name;

			// defensive programming - check if the data file exists
			// if it doesn't, then this folder isn't a profile and should be skipped
			string fullPath = Path.Combine(dataDirPath, profileId, dataFileName);
			if (!File.Exists(fullPath))
			{
				Debug.LogWarning("Skipping directory when loading all profiles because it does not contain data: "
					+ profileId);
				continue;
			}

			// load the game data for this profile and put it in the dictionary
			GameData profileData = Load(profileId);
			// defensive programming - ensure the profile data isn't null,
			// because if it is then something went wrong and we should let ourselves know
			if (profileData != null)
			{
				profileDictionary.Add(profileId, profileData);
			}
			else
			{
				Debug.LogError("Tried to load profile but something went wrong. ProfileId: " + profileId);
			}
		}

		return profileDictionary;
	}

	public string GetMostRecentlyUpdatedProfileId()
	{
		string mostRecentProfileId = null;

		Dictionary<string, GameData> profilesGameData = LoadAllProfiles();
		foreach (KeyValuePair<string, GameData> pair in profilesGameData)
		{
			string profileId = pair.Key;
			GameData gameData = pair.Value;

			// skip this entry if the gamedata is null
			if (gameData == null)
			{
				continue;
			}

			// if this is the first data we've come across that exists, it's the most recent so far
			if (mostRecentProfileId == null)
			{
				mostRecentProfileId = profileId;
			}
			// otherwise, compare to see which date is the most recent
			else
			{
				DateTime mostRecentDateTime = DateTime.FromBinary(profilesGameData[mostRecentProfileId].lastUpdated);
				DateTime newDateTime = DateTime.FromBinary(gameData.lastUpdated);
				// the greatest DateTime value is the most recent
				if (newDateTime > mostRecentDateTime)
				{
					mostRecentProfileId = profileId;
				}
			}
		}
		return mostRecentProfileId;
	}

	// AES encryption helpers (using passphrase-derived key). Not perfect for production; consider platform keystore.
	private byte[] EncryptToBytes(string plainText)
	{
		byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
		using (Aes aes = Aes.Create())
		{
			aes.Key = SHA256Hash(encryptionCodeWord);
			aes.GenerateIV();
			using (var ms = new MemoryStream())
			{
				// prepend IV
				ms.Write(aes.IV, 0, aes.IV.Length);
				using (var crypto = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
				{
					crypto.Write(plainBytes, 0, plainBytes.Length);
					crypto.FlushFinalBlock();
				}
				return ms.ToArray();
			}
		}
	}

	private string DecryptBytes(byte[] bytes)
	{
		using (Aes aes = Aes.Create())
		{
			aes.Key = SHA256Hash(encryptionCodeWord);
			int ivSize = aes.BlockSize / 8; // usually 16
			if (bytes.Length < ivSize) throw new Exception("Invalid encrypted data");
			byte[] iv = new byte[ivSize];
			Array.Copy(bytes, 0, iv, 0, ivSize);
			aes.IV = iv;

			using (var ms = new MemoryStream())
			{
				ms.Write(bytes, ivSize, bytes.Length - ivSize);
				ms.Position = 0;
				using (var crypto = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
				using (var sr = new StreamReader(crypto, Encoding.UTF8))
				{
					return sr.ReadToEnd();
				}
			}
		}
	}

	private byte[] SHA256Hash(string input)
	{
		using (SHA256 sha = SHA256.Create())
		{
			return sha.ComputeHash(Encoding.UTF8.GetBytes(input));
		}
	}

	private bool AttemptRollback(string fullPath)
	{
		bool success = false;
		string backupFilePath = fullPath + backupExtension;
		try
		{
			// if the backup exists, attempt to roll back to it by overwriting the original file
			if (File.Exists(backupFilePath))
			{
				File.Copy(backupFilePath, fullPath, true);
				success = true;
				Debug.LogWarning("Had to roll back to backup file at: " + backupFilePath);
			}
			else
			{
				Debug.LogWarning("Tried to roll back, but no backup file exists to roll back to.");
			}
		}
		catch (Exception e)
		{
			Debug.LogError("Error occured when trying to roll back to backup file at: "
				+ backupFilePath + "\n" + e);
		}

		return success;
	}
}
