using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FS_Core
{
    public class FSObjectDatabase<T> : MonoBehaviour where T : FSObject
    {
        Dictionary<string, T> objectDict = new Dictionary<string, T>();
        bool initialized = false;

        public void Init()
        {
            initialized = true;

            var objects = Resources.LoadAll<T>("");
            objectDict = new Dictionary<string, T>();

            foreach (var obj in objects)
            {
                if (objectDict.ContainsKey(obj.id))
                    obj.GenerateID();

                objectDict.Add(obj.id, obj);
            }
        }

        public T GetObjectById(string id)
        {
            if (!initialized)
                Init();

            if (objectDict.ContainsKey(id))
                return objectDict[id];
            else
            {
                Debug.LogError($"Could not find item with id {id}. Make sure it's inside a resource folder");
                return null;
            }
        }
    }
}
