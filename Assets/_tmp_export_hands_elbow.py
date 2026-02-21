import bpy
import bmesh
import os

SRC = r"S:\Projects\Slap_Prototype\SLAP\Assets\Animation\idle.fbx"
OUT_DIR = r"S:\Projects\Slap_Prototype\SLAP\Assets\Models\Hands"
os.makedirs(OUT_DIR, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_src(path):
    bpy.ops.import_scene.fbx(filepath=path)


def get_main_objects():
    arm = next((o for o in bpy.context.scene.objects if o.type == 'ARMATURE'), None)
    mesh = next((o for o in bpy.context.scene.objects if o.type == 'MESH'), None)
    if arm is None or mesh is None:
        raise RuntimeError('Armature or mesh not found in source FBX')
    return arm, mesh


def duplicate_object(obj, new_name):
    c = obj.copy()
    c.data = obj.data.copy()
    c.name = new_name
    bpy.context.collection.objects.link(c)
    return c


def keep_only_bones(arm_obj, keep_bones):
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm_obj.data.edit_bones
    for b in list(eb):
        if b.name not in keep_bones:
            eb.remove(b)
    bpy.ops.object.mode_set(mode='OBJECT')


def trim_mesh_by_weights(mesh_obj, allowed_groups, min_weight=0.0001):
    vg_index_to_name = {vg.index: vg.name for vg in mesh_obj.vertex_groups}

    bpy.context.view_layer.objects.active = mesh_obj
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(mesh_obj.data)

    to_delete = []
    dvert_lay = bm.verts.layers.deform.verify()

    for v in bm.verts:
        dv = v[dvert_lay]
        total_allowed = 0.0
        for gi, w in dv.items():
            name = vg_index_to_name.get(gi)
            if name in allowed_groups:
                total_allowed += w
        if total_allowed <= min_weight:
            to_delete.append(v)

    if to_delete:
        bmesh.ops.delete(bm, geom=to_delete, context='VERTS')

    bmesh.update_edit_mesh(mesh_obj.data)
    bpy.ops.object.mode_set(mode='OBJECT')


def prune_vertex_groups(mesh_obj, allowed_groups):
    for vg in list(mesh_obj.vertex_groups):
        if vg.name not in allowed_groups:
            mesh_obj.vertex_groups.remove(vg)


def relink_armature_modifier(mesh_obj, arm_obj):
    arm_mod = next((m for m in mesh_obj.modifiers if m.type == 'ARMATURE'), None)
    if arm_mod is None:
        arm_mod = mesh_obj.modifiers.new(name='Armature', type='ARMATURE')
    arm_mod.object = arm_obj


def export_pair(mesh_obj, arm_obj, out_path):
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj

    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=True,
        object_types={'ARMATURE', 'MESH'},
        add_leaf_bones=False,
        bake_anim=False,
        path_mode='AUTO'
    )


def build_side(base_arm, base_mesh, side):
    side_title = 'Left' if side == 'L' else 'Right'
    arm = duplicate_object(base_arm, f'{side_title}Armature')
    mesh = duplicate_object(base_mesh, f'{side_title}HandMesh')

    # Keep shoulder->arm->forearm->hand and finger chain for this side.
    keep_bones = {
        f'mixamorig:{side_title}Shoulder',
        f'mixamorig:{side_title}Arm',
        f'mixamorig:{side_title}ForeArm',
        f'mixamorig:{side_title}Hand',
        f'mixamorig:{side_title}HandIndex1',
        f'mixamorig:{side_title}HandIndex2',
    }

    allowed_groups = {
        f'mixamorig:{side_title}ForeArm',
        f'mixamorig:{side_title}Hand',
        f'mixamorig:{side_title}HandIndex1',
        f'mixamorig:{side_title}HandIndex2',
    }

    keep_only_bones(arm, keep_bones)
    trim_mesh_by_weights(mesh, allowed_groups)
    prune_vertex_groups(mesh, allowed_groups)
    relink_armature_modifier(mesh, arm)

    out_name = 'left_hand_elbow.fbx' if side == 'L' else 'right_hand_elbow.fbx'
    out_path = os.path.join(OUT_DIR, out_name)
    export_pair(mesh, arm, out_path)
    print('EXPORTED', out_path)


clear_scene()
import_src(SRC)
base_arm, base_mesh = get_main_objects()

build_side(base_arm, base_mesh, 'L')
build_side(base_arm, base_mesh, 'R')
