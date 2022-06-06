using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TypeD.Models.Interfaces;
using TypeD.Models.Providers.Interfaces;

namespace TypeDitor.Models
{
    internal class ResourceModel : IResourceModel, IModel
    {
        ResourceDictionary Resources { get; set; }

        // Constructors
        public ResourceModel(ResourceDictionary resources)
        {
            Resources = resources;
        }

        public void Init(IResourceModel resourceModel)
        {
            // This is not used
        }

        // Functions
        public void Add(List<object> values)
        {
            Add(values.Select(v => new Tuple<string, object>(v.GetType().Name, v)).ToList());
        }

        public void Add(object value)
        {
            Add(value.GetType().Name, value);
        }

        public void Add(List<Tuple<string, object>> keyValues)
        {
            foreach(var keyValue in keyValues)
            {
                if (!Resources.Contains(keyValue.Item1))
                    Resources.Add(keyValue.Item1, keyValue.Item2);
                else
                    Resources[keyValue.Item1] = keyValue.Item2;
            }

            foreach(var keyValue in keyValues)
            {
                Init(keyValue.Item2);
            }
        }

        public void Add(string key, object value)
        {
            if(!Resources.Contains(key))
                Resources.Add(key, value);
            else
                Resources[key] = value;

            Init(value);
        }

        private void Init(object value)
        {
            // If this is a Model or Provider then Init
            if (value is IModel)
            {
                (value as IModel).Init(this);
            }
            else if (value is IProvider)
            {
                (value as IProvider).Init(this);
            }
        }

        public void Remove(string key)
        {
            Resources.Remove(key);
        }

        public T Get<T>(string key) where T : class
        {
            if(!Resources.Contains(key))
                return null;
            return Resources[key] as T;
        }

        public T Get<T>() where T : class
        {
            var type = typeof(T);
            if(type.IsInterface && type.Name.StartsWith("I"))
            {
                return Get<T>(type.Name.Substring(1));
            }
            return Get<T>(type.Name);
        }
    }
}
