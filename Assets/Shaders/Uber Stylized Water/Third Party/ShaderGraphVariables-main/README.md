# Shader Graph Variables

- **Requires Shader Graph v10+ (Unity 2020.2+ onwards)**
- Should work for all pipelines. Built-in, URP, HDRP
- If there's any errors/problems in future versions, update to the newest version of the package. If problems persist, let me know by opening an [issue](https://github.com/Cyanilux/ShaderGraphVariables/issues) (if one doesn't already exist)

<img src="https://user-images.githubusercontent.com/69320946/122465638-3e384180-cfb0-11eb-8912-30dc00234a46.gif" width="600">

### Main Feature :
- Adds `Register Variable` and `Get Variable` nodes (technically empty subgraphs) to Shader Graph, allowing you to link sections of a graph without connection wires
  - `Register Variable` includes a TextField where you can enter a variable name (not case sensitive)
  - `Get Variable` includes a DropdownField (2021.2+) where you can select variables already registered. (2020.2 to 2021.1 will use another TextField where you can enter the same variable name). This will automatically link up the nodes with invisible connections/wires/edges
  - These variables are **local to the graph** - they won't be shared between other graphs or subgraphs
- Supports **Float**, **Vector2**, **Vector3** and **Vector4** types
- Variable names are serialized using the node's "synonyms" field, which is seemingly unused by the graph (only used in the Add Node menu). The field was added in v10 so is one main reason why the tool wouldn't work properly on previous versions.
- Note that if the package is removed, any graphs that used it should still load - but with errors as the `Register/Get Variable` SubGraphs used no longer exist. You'll need to manually remove these and reconnect those parts of the graph to get it working. Otherwise, reinstall the tool, or at least include the provided SubGraphs in your Assets to prevent errors.
- For setup & usage instructions, see below

### Extra Features :
- Group Colors
  - Right-click group name and select "Edit Group Color" to show Color Picker to edit background/border colour (inc. alpha)
  - Uses a hidden Color node & synonyms for serialization. It can't be moved so try to move the group as a whole rather than the nodes in it
<img src="https://user-images.githubusercontent.com/69320946/122465680-4b553080-cfb0-11eb-9f90-f6573de90084.gif" width="600">

- 'Port Swap' Hotkey (Default : S)
  - Swaps ports on selected nodes
  - Enabled for nodes : Add, Subtract, Multiply, Divide, Maximum, Minimum, Lerp, Inverse Lerp, Step and Smoothstep
<img src="https://user-images.githubusercontent.com/69320946/122465666-47c1a980-cfb0-11eb-918a-5e22c8423dde.gif" width="600">

- 'Add Node' Hotkeys (Default : Alpha Number keys, 1 to 0)
  - 10 hotkeys for quickly adding nodes at the mouse position. Defaults are :
    - 1 : Add
    - 2 : Subtract
    - 3 : Multiply
    - 4 : Lerp
    - 5 : Split
    - 6 : One Minus
    - 7 : Negate
    - 8 : Absolute
    - 9 : Step
    - 0 : Smoothstep
  - To change nodes : Tools → SGVariables → ExtraFeatures → Rebind Node Bindings
- To edit keybindings : Edit → Shortcuts (search for SGVariables)
  - Note, try to avoid conflicts with [SG's hotkeys](https://www.cyanilux.com/tutorials/intro-to-shader-graph/#shortcuts) (mainly A, F and O) as those can't be rebound

### Setup:
- Install via Package Manager → Add package via git URL : `https://github.com/Cyanilux/ShaderGraphVariables.git`
- Alternatively, download and put the folder in your Assets
- Note these methods won't update automatically, so please check back if you have any problems. If there's any important fixes or additional features added, I'll likely post about it on [twitter](https://twitter.com/Cyanilux) too!

### Usage : 
1) Add Node → `Register Variable`
    - The node has a Text Field in the place of it's output port where you can type a variable name
    - Attach a Float/Vector to the input port
2) Add Node → `Get Variable`
    - From 2020.2 to 2021.1 this node has a Text Field where you can type the same variable name.
    - While 2021.2 onwards, it now has a Dropdown Field where you can select variables (previously registered using the `Register Variable` node)
    - Variable names aren't case sensitive. "Example" would stil link to "EXAMPLE" or "eXaMpLe" etc.
    - When the variable name matches, the input port value (e.g. (0,0,0,0)) should disappear and the preview will change
    - A connection/edge may blink temporarily, but then is hidden to keep the graph clean (kinda the whole point of the tool)
    - Can now use the output of that node as you would usually

### Authors :
- Cyanilux ([URP/SG Tutorials](https://www.cyanilux.com/), [Twitter](https://twitter.com/Cyanilux))
- Also see contributors (on right side of github repo page) who fixed/adjusted some features

### Known Issues :
  - If a 'Get Variable' node is connected to the vertex stage, it can cause shader errors if fragment-only nodes are used by the variable (e.g. cannot map expression to vs_5_0 instruction set)
  - If a node uses a DynamicVector/DynamicValue slot (most math nodes) and it's output type changes, it won't update the type of Register Variable nodes that are already connected. The type of the variable node only changes when its port is changed.
