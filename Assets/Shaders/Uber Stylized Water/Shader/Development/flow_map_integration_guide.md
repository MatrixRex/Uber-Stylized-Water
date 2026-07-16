# Uber Stylized Water: Flow Map Integration Guide 🌊

This guide explains how to manually update the Shader Graphs and Subgraphs to support direct UV2 flow mapping.

---

## Part 1: Update the Subgraphs

### 1. Update `2Way Normal Blend.shadersubgraph`
1. Open `Assets/Shaders/Uber Stylized Water/SubGraphs/2Way Normal Blend.shadersubgraph` in the Shader Graph editor.
2. In the **Blackboard**, add a new property:
   - Type: **Vector2**
   - Display Name: **FlowDir**
   - Reference: **FlowDir**
3. Locate the **TwoWayNormalBlend (Custom Function)** node in the graph.
4. Open the node's settings (gear icon) and add a new input port:
   - Name: **FlowDir**
   - Type: **Vector2**
5. Connect the **FlowDir** property from the blackboard to the new **FlowDir** input port of the custom function node.
6. Save the subgraph.

### 2. Update `CausticsGenerator.shadersubgraph`
1. Open `Assets/Shaders/Uber Stylized Water/SubGraphs/CausticsGenerator.shadersubgraph` in the Shader Graph editor.
2. In the **Blackboard**, add a new property:
   - Type: **Vector2**
   - Display Name: **FlowDir**
   - Reference: **FlowDir**
3. Locate the **TwoWayCaustics (Custom Function)** node in the graph.
4. Open the node's settings (gear icon) and add a new input port:
   - Name: **FlowDir**
   - Type: **Vector2**
5. Connect the **FlowDir** property from the blackboard to the new **FlowDir** input port of the custom function node.
6. Save the subgraph.

---

## Part 2: Update the Main Shader Graph

Open `Assets/Shaders/Uber Stylized Water/Shader/Development/UberStylizedWaterGraph.shadergraph` in the Shader Graph editor.

### 1. Add Flow Debug Visualization Property
In the **Blackboard**, add a new property:
- Type: **Float** (or Slider/Integer)
- Display Name: **ShowFlowDirection**
- Reference: **_ShowFlowDirection**
- Default Value: **0** (disabled)

### 2. Retrieve and Resolve Flow Direction
We need to read the mesh's `uv2` coordinates (where flow direction is stored) and convert them to world space if using world-space (open water) coords.
1. Create a **UV** node. Set its channel to **UV1** (representing UV2 in Unity).
2. Create a **Split** node and connect the **UV1** output to it.
3. Create a **Vector2** node and connect the `.x` and `.y` outputs of the split node to it. This represents the raw **FlowDir** vector.
4. Create a **Tangent Vector** node (Space: World).
5. Create a **Bitangent Vector** node (Space: World).
6. Multiply the **Tangent Vector** by the `.x` component of `UV1` (using a **Multiply** node).
7. Multiply the **Bitangent Vector** by the `.y` component of `UV1` (using a **Multiply** node).
8. Add the two multiplied vectors together (using an **Add** node) to get the world-space flow vector.
9. Create a **Split** node, connect the added world-space flow vector, and feed its `.x` and `.z` components into a **Vector2** node. This represents the **WorldSpace XZ FlowDir**.
10. Drag the **UseMeshUV** property from the Blackboard.
11. Create a **Comparison** node: set Input A to **UseMeshUV**, Input B to **True** (check the box), and Type to **Equal**.
12. Create a **Branch** node:
    - Connect the comparison result to the **Predicate** input.
    - Connect the local **FlowDir** (from step 3) to the **True** input.
    - Connect the **WorldSpace XZ FlowDir** (from step 9) to the **False** input.
13. The output of the **Branch** node is our **ResolvedFlowDir**. (Create a group or label it for easy routing).

### 3. Connect Flow Dir to Normals and Caustics
1. Find the **2Way Normal Blend** subgraph node in the main graph.
   - Connect **ResolvedFlowDir** to its new **FlowDir** input port.
2. Find the **Caustics Generator** subgraph node in the main graph.
   - Connect **ResolvedFlowDir** to its new **FlowDir** input port.

### 4. Connect Flow Dir to Surface Foam Panning
We will redirect the foam panning speed using our helper custom function:
1. Create a **Custom Function** node. Set its properties:
   - Name: **AdjustFoamPanSpeed**
   - Source: `Assets/Shaders/Uber Stylized Water/Shader/Development/FlowMapHelpers.hlsl`
   - Function Name: **AdjustFoamPanSpeed**
   - Add Inputs:
     - **DefaultPanSpeed** (Vector 2)
     - **ResolvedFlowDir** (Vector 2)
   - Add Outputs:
     - **OutPanSpeed** (Vector 2)
2. Locate the **SurfFoam_Pan** property node.
3. Connect **SurfFoam_Pan** to **DefaultPanSpeed** of the custom function.
4. Connect **ResolvedFlowDir** to **ResolvedFlowDir** of the custom function.
5. Find the **Surface Foam Generator** subgraph node in the main graph.
6. Disconnect `SurfFoam_Pan` from it and instead connect the **OutPanSpeed** of your custom function to the **SurfFoam_Pan** input port.

*Note: You can do the exact same for **SurfaceDistortion_Pan** or **InterSec_Foam_Pan** if you want them to also pan in the flow direction.*

### 5. Add Flow Visualizer before Final Output
We will inject the visualizer right before the final color output:
1. Create a **Custom Function** node. Set its properties:
   - Name: **ShowFlowDirection**
   - Source: `Assets/Shaders/Uber Stylized Water/Shader/Development/FlowMapHelpers.hlsl`
   - Function Name: **ShowFlowDirection**
   - Add Inputs:
     - **FlowDir** (Vector 2)
     - **ShowFlow** (Float)
     - **BaseColor** (Vector 3)
   - Add Outputs:
     - **OutColor** (Vector 3)
2. Connect **ResolvedFlowDir** to the **FlowDir** input.
3. Drag the **ShowFlowDirection** property node (`_ShowFlowDirection`) from the Blackboard and connect it to the **ShowFlow** input.
4. Locate the final color node in your graph that normally connects to the **Base Color** or **Emission** input of your Master Stack.
5. Connect that final color to the **BaseColor** input of the custom function node.
6. Connect the **OutColor** output of the custom function node to the corresponding **Base Color** (or **Emission**) input of the Master Stack.

Save the main Shader Graph. You're all set!
