using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.EditorCoroutines.Editor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.UIElements;

namespace PackageGenerationTool.Editor
{
    public enum DeploymentType
    {
        LOCAL,
        EMBEDDED
    }

    [Serializable]
    public class SerializableDependencyInfo
    {
        public string name;
        public string version;
    }

    public class SerializablePackageInfoConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            SerializablePackageInfo pkg = (SerializablePackageInfo)value;

            writer.WriteStartObject();
            writer.WritePropertyName("name");
            writer.WriteValue(pkg.Name);
            writer.WritePropertyName("version");
            writer.WriteValue(pkg.Version);
            if (!string.IsNullOrEmpty(pkg.DisplayName))
            {
                writer.WritePropertyName("displayName");
                writer.WriteValue(pkg.DisplayName);
            }
            if (!string.IsNullOrEmpty(pkg.Description))
            {
                writer.WritePropertyName("description");
                writer.WriteValue(pkg.Description);
            }
            if(!string.IsNullOrEmpty(pkg.Author))
            {
                writer.WritePropertyName("author");
                writer.WriteValue(pkg.Author);
            }
            if (!string.IsNullOrEmpty(pkg.Unity))
            {
                writer.WritePropertyName("unity");
                writer.WriteValue(pkg.Unity);
            }
            if(pkg.Dependencies != null
                && pkg.Dependencies.Count > 0)
            {
                writer.WritePropertyName("dependencies");
                writer.WriteStartObject();
                foreach (SerializableDependencyInfo sdi in pkg.Dependencies)
                {
                    writer.WritePropertyName(sdi.name);
                    writer.WriteValue(sdi.version);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(SerializablePackageInfo);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    [JsonConverter(typeof(SerializablePackageInfoConverter))]

    [Serializable]
    public class SerializablePackageInfo
    {
        public string Name;
        public string Version;
        public string DisplayName;
        public string Description;
        public string Author;
        public string Unity;
        public List<SerializableDependencyInfo> Dependencies;
    }

    public class SerializableAssemblyDefinitionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(SerializableAssemblyDefinition);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            SerializableAssemblyDefinition asmdef = (SerializableAssemblyDefinition)value;

            //"name": "Unity.EditorCoroutines.Editor",
            //"references": [],
            //"optionalUnityReferences": [],
            //"includePlatforms": [
            //    "Editor"
            //],
            //"excludePlatforms": [],
            //"allowUnsafeCode": false,
            //"overrideReferences": false,
            //"precompiledReferences": [],
            //"autoReferenced": true,
            //"defineConstraints": []

            writer.WriteStartObject();
            writer.WritePropertyName("name");
            writer.WriteValue(asmdef.Name);
            writer.WritePropertyName("rootNamespace");
            writer.WriteValue(asmdef.RootNamespace);
            writer.WritePropertyName("references");
            writer.WriteStartArray();writer.WriteEndArray();
            writer.WritePropertyName("optionalUnityReferences");
            writer.WriteStartArray(); writer.WriteEndArray();
            writer.WritePropertyName("excludePlatforms");
            writer.WriteStartArray(); writer.WriteEndArray();
            writer.WritePropertyName("precompiledReferences");
            writer.WriteStartArray(); writer.WriteEndArray();
            writer.WritePropertyName("defineConstraints");
            writer.WriteStartArray(); writer.WriteEndArray();
            writer.WritePropertyName("includePlatforms");
            writer.WriteStartArray();
            if(asmdef.includePlatforms != null
                && asmdef.includePlatforms.Length > 0)
            {
                foreach (string platform in asmdef.includePlatforms)
                {
                    writer.WriteValue(platform);
                }
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }

    [JsonConverter(typeof(SerializableAssemblyDefinitionConverter))]
    [Serializable]
    public class SerializableAssemblyDefinition
    {
        public string Name;
        public string RootNamespace;

        public string[] includePlatforms;
    }

    [Serializable]
    public class PackageInfoProxy
    {
        public PackageInfo Info;
        public bool Selected;
        public string VersionOverride;
    }

    public class PackageGenerationToolEditor : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;
        [SerializeField]
        private StyleSheet m_StyleSheet = default;

        private static PackageGenerationToolEditor window;
        private static GUIContent window_title = new GUIContent("Package Generation Tool");
        private static List<PackageInfoProxy> packages = new List<PackageInfoProxy>();

        private static EditorCoroutine refresh_packages_coro = null;

        private TextField package_name_textfield;
        private TextField company_textfield;
        private TextField package_author_textfield;
        private TextField description_textfield;
        private TextField unity_min_version_textfield;
        private TextField root_namespace_textfield;

        private RadioButtonGroup deployment_type;
        //private ListView dependencies_list;
        private MultiColumnListView dependencies_list;

        private TextField override_package_name_textfield;
        private string override_package_name_cached;
        private Toggle override_package_name_toggle;

        private TextField override_package_display_name_textfield;
        private string override_package_display_name_cached;
        private Toggle override_package_display_name_toggle;

        private TextField override_root_namespace_textfield;
        private string override_root_namespace_cached;
        private Toggle override_root_namespace_toggle;

        private Button refresh_packages_btn;
        private Button generate_package_btn;

        [MenuItem("Package Tools/Generate Package Tool")]
        public static void ShowMyEditor()
        {
            // This method is called when the user selects the menu item in the Editor
            window = GetWindow<PackageGenerationToolEditor>(new Type[] { typeof(SceneView) });
            window.titleContent = window_title;
        }

        public void CreateGUI()
        {
            if (m_VisualTreeAsset == null) return;

            VisualElement root = rootVisualElement;

            m_VisualTreeAsset.CloneTree(root);
            root.styleSheets.Add(m_StyleSheet);

            {//References
                package_name_textfield = root.Q<TextField>("package-name");
                company_textfield = root.Q<TextField>("company-name");
                package_author_textfield = root.Q<TextField>("package-author");
                description_textfield = root.Q<TextField>("package-description");
                unity_min_version_textfield = root.Q<TextField>("unity-min-version");
                deployment_type = root.Q<RadioButtonGroup>("deployment-type");
                override_package_name_textfield = root.Q<TextField>("override-fully-qualified-package-name");
                override_package_name_toggle = root.Q<Toggle>("override-fully-qualified-package-name-toggle");
                override_package_display_name_textfield = root.Q<TextField>("override-package-displayname");
                override_package_display_name_toggle = root.Q<Toggle>("override-package-displayname-toggle");
                override_root_namespace_textfield = root.Q<TextField>("override-root-namespace");
                override_root_namespace_toggle = root.Q<Toggle>("override-root-namespace-toggle");
                root_namespace_textfield = root.Q<TextField>("root-namespace");
                refresh_packages_btn = root.Q<Button>("refresh-packages-btn");
                generate_package_btn = root.Q<Button>("generate-package-btn");
            }

            {//Initialization
                //package_name_textfield.value = "";
                //company_textfield.value = "";
                package_author_textfield.value = "";
                description_textfield.value= "";
                unity_min_version_textfield.value = "";
                deployment_type.value = 0;
            }

            {//Override Package Name initialization
                override_package_name_toggle.value = false;
                override_package_name_textfield.isReadOnly = true;
                override_package_name_textfield.style.opacity = new StyleFloat(0.2f);
                override_package_name_textfield.value = RegenerateFullyQualifiedPackageName();
                override_package_name_toggle.RegisterCallback<ChangeEvent<bool>>(OnOverridePackageNameToggleChanged);

                package_name_textfield.RegisterCallback<ChangeEvent<string>>(OnPackageNameOrCompanyNameChanged);
                company_textfield.RegisterCallback<ChangeEvent<string>>(OnPackageNameOrCompanyNameChanged);
            }

            {//Override Package Display Name initialization
                override_package_display_name_toggle.value = false;
                override_package_display_name_textfield.isReadOnly = true;
                override_package_display_name_textfield.style.opacity = new StyleFloat(0.2f);
                override_package_display_name_textfield.value = RegeneratePackageDisplayName();
                override_package_display_name_toggle.RegisterCallback<ChangeEvent<bool>>(OnOverridePackageDisplayNameToggleChanged);

                package_name_textfield.RegisterCallback<ChangeEvent<string>>(OnPackageNameOrCompanyNameChanged);
                company_textfield.RegisterCallback<ChangeEvent<string>>(OnPackageNameOrCompanyNameChanged);
            }

            {//Override Root Namespace initialization
                override_root_namespace_toggle.value = false;
                override_root_namespace_textfield.isReadOnly = true;
                override_root_namespace_textfield.style.opacity = new StyleFloat(0.2f);
                override_root_namespace_textfield.value = RegeneratePackageDisplayName();
                override_root_namespace_toggle.RegisterCallback<ChangeEvent<bool>>(OnOverrideRootNamespaceToggleChanged);

                package_name_textfield.RegisterCallback<ChangeEvent<string>>(OnPackageNameOrCompanyNameChanged);
                company_textfield.RegisterCallback<ChangeEvent<string>>(OnPackageNameOrCompanyNameChanged);
            }

            //{
            //    // The "makeItem" function is called when the
            //    // ListView needs more items to render.
            //    Func<VisualElement> makeItem = () =>
            //    {
            //        VisualElement row = new VisualElement();
            //        Label package_label = new Label();
            //        package_label.name = "package-name";
            //        package_label.bindingPath = "Info.displayName";
            //        row.Add(package_label);
            //        Toggle row_toggle = new Toggle();
            //        row_toggle.name = "package-selected";
            //        row_toggle.bindingPath = "Selected";
            //        row.Add(row_toggle);

            //        row.style.flexShrink = 1;
            //        row.style.flexGrow = 1;
            //        return row;
            //    };

            //    // As the user scrolls through the list, the ListView object
            //    // recycles elements created by the "makeItem" function,
            //    // and invoke the "bindItem" callback to associate
            //    // the element with the matching data item (specified as an index in the list).
            //    Action<VisualElement, int> bindItem = (e, i) =>
            //    {
            //        VisualElement row_root = (e as VisualElement);
            //        row_root.Q<Label>().text = packages[i].Info.displayName;
            //        Toggle row_toggle = row_root.Q<Toggle>();
            //        row_toggle.value = false;
            //        row_toggle.userData = i;
            //        row_toggle.RegisterValueChangedCallback<bool>(OnDependencySelected);
            //    };
            //    // Provide the list view with an explicit height for every row
            //    // so it can calculate how many items to actually display
            //    const int itemHeight = 16;

            //    dependencies_list = root.Q<ListView>("package-dependencies");
            //    dependencies_list.itemsSource = packages;
            //    dependencies_list.fixedItemHeight = itemHeight;
            //    dependencies_list.makeItem = makeItem;
            //    dependencies_list.bindItem = bindItem;
            //    dependencies_list.selectionType = SelectionType.Multiple;
            //    dependencies_list.RefreshItems();
            //}

            {
                dependencies_list = root.Q<MultiColumnListView>();
                // Call MultiColumnTreeView.SetRootItems() to populate the data in the tree.
                dependencies_list.itemsSource = packages;

                // For each column, set Column.makeCell to initialize each node in the tree.
                // You can index the columns array with names or numerical indices.
                dependencies_list.columns["selected"].makeCell = () => new Toggle();
                dependencies_list.columns["package-name"].makeCell = () => new Label();
                dependencies_list.columns["package-version-override"].makeCell = () => new TextField();

                // For each column, set Column.bindCell to bind an initialized node to a data item.
                dependencies_list.columns["selected"].bindCell = (VisualElement element, int index) =>
                {
                    Toggle t = (element as Toggle);
                    t.value = packages[index].Selected;
                    t.userData = index;
                    t.RegisterValueChangedCallback<bool>(OnDependencySelected);
                };
                dependencies_list.columns["package-name"].bindCell = (VisualElement element, int index) =>
                    (element as Label).text = packages[index].Info.displayName;
                dependencies_list.columns["package-version-override"].bindCell = (VisualElement element, int index) =>
                {
                    TextField t = (element as TextField);
                    t.SetValueWithoutNotify(packages[index].VersionOverride);
                    t.userData = index;
                    t.RegisterValueChangedCallback<string>(OnDependencyVersionOverriden);
                };

                ToolbarSearchField dep_search = root.Q<ToolbarSearchField>();
                dep_search.Q<Button>().clicked += ()=> {
                    if (!string.IsNullOrEmpty(dep_search.value))
                    {
                        for(int i = 0; i < packages.Count; i++)
                        {
                            if (packages[i].Info.displayName.ToLower().Contains(dep_search.value.ToLower()))
                            {
                                dependencies_list.ScrollToItem(i);
                                dependencies_list.selectedIndex = i;
                                break;
                            }
                        }
                    }
                };
                dep_search.RegisterValueChangedCallback(evt => Debug.Log("New search value: " + evt.newValue));
            }

            {//RefreshPackages
                refresh_packages_btn.clicked += OnRefreshPackagesClicked;
            }

            {//Generate
                generate_package_btn.clicked += OnGenerateClicked;
            }


            if(refresh_packages_coro != null)
            {
                EditorCoroutineUtility.StopCoroutine(refresh_packages_coro);
                refresh_packages_coro = null;
            }
            refresh_packages_coro = EditorCoroutineUtility.StartCoroutine(RefreshPackageList(), this);
            //Highlighter.Highlight(window.name, "tool-panel");
        }

        IEnumerator RefreshPackageList(bool only_remote = true)
        {
            if(!only_remote)
            {
                ListRequest package_list_request = Client.List(false);
                while (!package_list_request.IsCompleted)
                {
                    yield return new EditorWaitForSeconds(1.0f);
                }
                
                PackageCollection built_in = package_list_request.Result;

                foreach (PackageInfo pkg_info in built_in)
                {
                    packages.Add(new PackageInfoProxy()
                    {
                        Info = pkg_info,
                        Selected = false,
                        VersionOverride = pkg_info.version
                    });
                }
            }

            {
                SearchRequest package_list_request = Client.SearchAll(false);
                while (!package_list_request.IsCompleted)
                {
                    yield return new EditorWaitForSeconds(1.0f);
                }
                PackageInfo[] remote_packages = package_list_request.Result;

                foreach (PackageInfo pkg_info in remote_packages)
                {
                    packages.Add(new PackageInfoProxy()
                    {
                        Info = pkg_info,
                        Selected = false,
                        VersionOverride = pkg_info.version
                    });
                }
            }

            dependencies_list.RefreshItems();
        }

        private void OnRefreshPackagesClicked()
        {
            if (refresh_packages_coro != null)
            {
                EditorCoroutineUtility.StopCoroutine(refresh_packages_coro);
                refresh_packages_coro = null;
            }
            refresh_packages_coro = EditorCoroutineUtility.StartCoroutine(RefreshPackageList(), this);
        }

        private void OnPackageNameOrCompanyNameChanged(ChangeEvent<string> evt)
        {
            if (!override_package_name_toggle.value)
            {
                string new_name = RegenerateFullyQualifiedPackageName();
                override_package_name_textfield.value = new_name;
            }

            if (!override_package_display_name_toggle.value)
            {
                string new_name = RegeneratePackageDisplayName();
                override_package_display_name_textfield.value = new_name;
            }

            if (!override_root_namespace_toggle.value)
            {
                string new_name = RegeneratePackageDisplayName();
                override_root_namespace_textfield.value = new_name;
            }
        }

        private void OnOverridePackageNameToggleChanged(ChangeEvent<bool> b)
        {
            override_package_name_textfield.isReadOnly = !b.newValue;
            override_package_name_textfield.style.opacity = new StyleFloat((!b.newValue) ? 0.2f : 1.0f);
            if (!b.newValue)
            {
                override_package_name_cached = override_package_name_textfield.value;
                override_package_name_textfield.value = RegenerateFullyQualifiedPackageName();
            }
            else
            {
                if (string.IsNullOrEmpty(override_package_name_cached))
                {
                    override_package_name_cached = RegenerateFullyQualifiedPackageName();
                }
                override_package_name_textfield.value = override_package_name_cached;
            }
        }

        private void OnOverridePackageDisplayNameToggleChanged(ChangeEvent<bool> b)
        {
            override_package_display_name_textfield.isReadOnly = !b.newValue;
            override_package_display_name_textfield.style.opacity = new StyleFloat((!b.newValue) ? 0.2f : 1.0f);
            if (!b.newValue)
            {
                override_package_display_name_cached = override_package_display_name_textfield.value;
                override_package_display_name_textfield.value = RegeneratePackageDisplayName();
            }
            else
            {
                if (string.IsNullOrEmpty(override_package_display_name_cached))
                {
                    override_package_display_name_cached = RegeneratePackageDisplayName();
                }
                override_package_display_name_textfield.value = override_package_display_name_cached;
            }
        }

        private void OnOverrideRootNamespaceToggleChanged(ChangeEvent<bool> b)
        {
            override_root_namespace_textfield.isReadOnly = !b.newValue;
            override_root_namespace_textfield.style.opacity = new StyleFloat((!b.newValue) ? 0.2f : 1.0f);
            if (!b.newValue)
            {
                override_root_namespace_cached = override_root_namespace_textfield.value;
                override_root_namespace_textfield.value = RegeneratePackageDisplayName();
            }
            else
            {
                if (string.IsNullOrEmpty(override_root_namespace_cached))
                {
                    override_root_namespace_cached = RegeneratePackageDisplayName();
                }
                override_root_namespace_textfield.value = override_root_namespace_cached;
            }
        }

        private string RegenerateFullyQualifiedPackageName()
        {
            string fully_qualified_package_name = string.Format($"com.{company_textfield.value}.{package_name_textfield.value}");
            return fully_qualified_package_name.ToLower();
        }

        private void OnDependencySelected(ChangeEvent<bool> evt)
        {
            Toggle t = (Toggle)evt.target;
            PackageInfoProxy pip = packages[(int)t.userData];
            pip.Selected = evt.newValue;
        }

        private void OnDependencyVersionOverriden(ChangeEvent<string> evt)
        {
            TextField t = (TextField)evt.target;
            PackageInfoProxy pip = packages[(int)t.userData];
            pip.VersionOverride = evt.newValue;
        }

        private string RegeneratePackageDisplayName()
        {
            string package_display_name = string.Format($"{company_textfield.value}.{package_name_textfield.value}");
            return package_display_name;
        }

        private void OnGenerateClicked()
        {
            string package_name = package_name_textfield.value;
            string company_name = company_textfield.value;
            string author = package_author_textfield.value;
            string root_namespace = override_root_namespace_textfield.value;
            string package_display_name = override_package_display_name_textfield.value;
            string package_description = description_textfield.value;
            string unity_min_version = unity_min_version_textfield.value;
            string fully_qualified_name = override_package_name_textfield.value;

            DeploymentType deploy = (DeploymentType)deployment_type.value;

            SerializablePackageInfo spi = new SerializablePackageInfo()
            {
                Name = fully_qualified_name,
                Version = "0.0.1",
                DisplayName = package_display_name,
                Description = package_description,
                Author = author,
                Unity = unity_min_version,
                Dependencies = GeneratePackageDependencyList()
            };

            DirectoryInfo project_root_di = new DirectoryInfo(Application.dataPath).Parent;
            DirectoryInfo package_root_di = null;
            {//Create the package folder first
                if (deploy == DeploymentType.LOCAL)
                {
                    package_root_di = Directory.CreateDirectory(Path.Combine(project_root_di.FullName, fully_qualified_name));
                }
                else if (deploy == DeploymentType.EMBEDDED)
                {
                    string packages_root = Path.Combine(project_root_di.FullName, "Packages");
                    package_root_di = Directory.CreateDirectory(Path.Combine(packages_root, fully_qualified_name));
                }
            }

            {
                //Create subfolders Editor
                DirectoryInfo editor_di = package_root_di.CreateSubdirectory("Editor");
                {//Create asmdef
                    SerializableAssemblyDefinition sed = new SerializableAssemblyDefinition()
                    {
                        Name = company_name+"."+package_name+".Editor",
                        RootNamespace = root_namespace+".Editor",
                        includePlatforms = new string[]
                        {
                            "Editor"
                        }
                    };

                    string json = JsonConvert.SerializeObject(sed, Formatting.Indented);
                    string file_path = Path.Combine(editor_di.FullName, sed.Name+".asmdef");
                    File.WriteAllText(file_path, json);
                }
            }

            {//Create Runtime
                DirectoryInfo runtime_di = package_root_di.CreateSubdirectory("Runtime");
                {//Create asmdef
                    SerializableAssemblyDefinition sed = new SerializableAssemblyDefinition()
                    {
                        Name = company_name + "." + package_name + ".Runtime",
                        RootNamespace = root_namespace
                    };

                    string json = JsonConvert.SerializeObject(sed, Formatting.Indented);
                    string file_path = Path.Combine(runtime_di.FullName, sed.Name + ".asmdef");
                    File.WriteAllText(file_path, json);
                }
            }

            {//Generate package.json
                string json = JsonConvert.SerializeObject(spi, Formatting.Indented);
                string file_path = Path.Combine(package_root_di.FullName, "package.json");
                File.WriteAllText(file_path, json);
            }

            {//Install
                if (deploy == DeploymentType.LOCAL)
                {
                    AddRequest req = Client.Add("file:" + package_root_di.FullName.Replace("\\", "/"));
                    while (!req.IsCompleted)
                    {
                    }
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                }
                Client.Resolve();
            }
        }

        private List<SerializableDependencyInfo> GeneratePackageDependencyList()
        {
            List<SerializableDependencyInfo> package_dependencies = new List<SerializableDependencyInfo>();
            foreach(PackageInfoProxy pip in packages)
            {
                if (!pip.Selected) continue;
                SerializableDependencyInfo dependency = new SerializableDependencyInfo()
                {
                    name = pip.Info.name,
                    version = pip.VersionOverride
                };

                package_dependencies.Add(dependency);
            }
            return package_dependencies;
        }
    }
}
