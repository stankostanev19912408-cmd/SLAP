import bpy
import os

src = r"S:\Projects\Slap_Prototype\SLAP\Assets\Animation\idle.fbx"

# reset scene
bpy.ops.wm.read_factory_settings(use_empty=True)

bpy.ops.import_scene.fbx(filepath=src)

print('OBJECTS:')
for o in bpy.context.scene.objects:
    print(o.name, o.type)

for a in [o for o in bpy.context.scene.objects if o.type=='ARMATURE']:
    print('ARMATURE', a.name)
    for b in a.data.bones:
        print('BONE', b.name)

for m in [o for o in bpy.context.scene.objects if o.type=='MESH']:
    print('MESH', m.name, 'vgs=', len(m.vertex_groups))
    for vg in m.vertex_groups[:20]:
        print('VG', vg.name)
