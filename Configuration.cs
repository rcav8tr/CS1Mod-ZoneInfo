using System;
using System.Linq;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

/// <summary>
/// Handle loading and saving a global configuration file for a mod
/// Based on code by boformer:
/// https://gist.githubusercontent.com/boformer/cb6840867c6febd25c8f/raw/a56159664b974be4b3e7d6625d08bc35b7a3f9a6/Configuration.cs
/// By default, the config file is saved in the same location as Cities.exe.  For Windows, this is:
/// C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines
/// </summary>
/// <typeparam name="C">configuration type</typeparam>
public abstract class Configuration<C> where C : class, new()
{
    // the one and only instance of the config class to be loaded/saved
    private static C instance;

    /// <summary>
    /// Return the config class from the file
    /// </summary>
    /// <returns>an instance of the config class from the config file or a new instance of the config class</returns>
    public static C Load()
    {
        // check for an instance
        if (instance == null)
        {
            try
            {
                // check if the config file exists
                string configFile = GetConfigFile();
                if (File.Exists(configFile))
                {
                    // load the config file into a the instance
                    using (StreamReader streamReader = new StreamReader(configFile))
                    {
                        XmlSerializer xmlSerializer = new XmlSerializer(typeof(C));
                        instance = xmlSerializer.Deserialize(streamReader) as C;
                    }
                }
                else
                {
                    // no config file, create a new instance of the config class
                    instance = new C();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                instance = new C();
            }
        }

        // return the instance (either loaded or new)
        return instance;
    }

    /// <summary>
    /// Save the instance to the config file
    /// </summary>
    public static void Save()
    {
        // the config must have been loaded prior to attempting to save
        if (instance == null)
        {
            Debug.LogError("Attempt to save configuration for " + typeof(C).Name + " before an instance was loaded.");
            return;
        }

        try
        {
            // save the instance to the config file
            string configFile = GetConfigFile();
            using (StreamWriter streamWriter = new StreamWriter(configFile))
            {
                XmlSerializerNamespaces noNamespaces = new XmlSerializerNamespaces();
                noNamespaces.Add("", "");
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(C));
                xmlSerializer.Serialize(streamWriter, instance, noNamespaces);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    /// <summary>
    /// get the config file name from the attribute
    /// </summary>
    /// <returns>config file name</returns>
    private static string GetConfigFile()
    {
        // get the attribute
        ConfigurationFileNameAttribute configFileAttribute = typeof(C).GetCustomAttributes(typeof(ConfigurationFileNameAttribute), true)
            .FirstOrDefault() as ConfigurationFileNameAttribute;
        if (configFileAttribute != null)
        {
            // found the attribute, return it
            return configFileAttribute.Value;
        }
        else
        {
            // log an error and return a default file name
            Debug.LogError("ConfigurationFile attribute missing in " + typeof(C).Name);
            return typeof(C).Name + "Config.xml";
        }
    }
}

/// <summary>
/// the configuration file name attribute
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ConfigurationFileNameAttribute : Attribute
{
    public ConfigurationFileNameAttribute(string value)
    {
        Value = value;
    }

    public string Value { get; private set; }
}
