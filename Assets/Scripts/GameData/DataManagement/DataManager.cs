using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public abstract class DataManager<T, D> : Singleton<T>, IDataPersistence, IDataManager<D> where T : DataManager<T, D> where D : Data
{
	protected SerializableDictionary<int, D> LocalData;

	protected DataManager()
	{
		LocalData = new SerializableDictionary<int, D>();
	}

	public virtual void LoadData(GameData persistantData)
	{
		throw new System.NotImplementedException();
	}

	public virtual void SaveData(GameData persistantData)
	{
		throw new System.NotImplementedException();
	}

	public D GetData(int accessor)
	{
		return LocalData[accessor];
	}

	public List<D> GetAllData()
	{
		return LocalData.Values.ToList();
	}

	public void AddNewData(D newData)
	{
		int id = GenerateUniqueAccessorId(LocalData.Keys.ToList());
		newData.AccessorId = id;
		LocalData.Add(id, newData);
	}

	protected int GenerateUniqueAccessorId(List<int> existingIds)
	{
		int newId = Random.Range(1, int.MaxValue);

		while (existingIds.Contains(newId))
		{
			newId = Random.Range(1, int.MaxValue);
		}

		return newId;
	}
}

public interface IDataManager<D>
{
	public abstract D GetData(int accessor);
	public abstract List<D> GetAllData();
}

public abstract class Data
{
	public int AccessorId;
}