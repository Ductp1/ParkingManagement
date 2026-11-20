"""
Xuất mô hình bãi đỗ (pk.blend) sang GLB cho tính năng "Xem 3D đường đến ô đã đặt" (trang tài xế).

Chạy (Blender 4.x / 5.x, KHÔNG lưu lại file .blend):
    blender -b "D:/My_Project/Blender-Parking/pk.blend" --python tools/blender/export_pk.py -- <đường_dẫn_ra.glb>
Mặc định ghi vào Frontend/ParkingManagement.Web/public/models/lots/1/parking.glb (bãi Vincom – lot 1).

Script:
  * gắn custom property "floor" (1, 2, 3) cho từng vật thể theo cao độ để viewer ẩn các tầng phía trên tầng đích;
  * áp modifier (ram xoắn ốc dùng SCREW) khi xuất, bỏ camera / đèn;
  * nhúng ảnh bản vẽ sàn vào GLB; ảnh nào không tìm thấy trên máy thì sàn đó xuất không có ảnh (in cảnh báo).
"""
import os
import sys

import bpy

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_OUT = os.path.join(REPO, "Frontend", "ParkingManagement.Web", "public", "models", "lots", "1", "parking.glb")

# Cao độ mặt sàn (m, trục Z của Blender): tầng 1 ≈ 0.2, tầng 2 ≈ 6.0, tầng 3 ≈ 12.3.
FLOOR_BREAKS = [(5.5, 1), (11.8, 2)]


def floor_of(obj):
    """Tầng của vật thể theo đáy hộp bao (vật cao hơn 3 m như ram / tường được tính theo đáy)."""
    corners = [obj.matrix_world @ v.co for v in obj.data.vertices] if obj.type == "MESH" and obj.data.vertices else []
    bottom = min(c.z for c in corners) if corners else obj.matrix_world.translation.z
    for limit, floor in FLOOR_BREAKS:
        if bottom < limit:
            return floor
    return 3


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.abspath(argv[0]) if argv else DEFAULT_OUT
    os.makedirs(os.path.dirname(out), exist_ok=True)

    missing = [img.name for img in bpy.data.images
               if img.source == "FILE" and img.filepath and not os.path.exists(bpy.path.abspath(img.filepath))]
    for name in missing:
        print(f"[export_pk] CẢNH BÁO: không tìm thấy ảnh '{name}' – vật liệu dùng ảnh này sẽ không có texture.")

    for obj in bpy.data.objects:
        obj["floor"] = floor_of(obj)
        obj.hide_viewport = False
        obj.hide_set(False)
        obj.hide_render = False

    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj.select_set(True)

    bpy.ops.export_scene.gltf(
        filepath=out,
        export_format="GLB",
        use_selection=True,
        export_apply=True,          # áp SCREW modifier của ram
        export_extras=True,         # custom property "floor" → userData trong three.js
        export_cameras=False,
        export_lights=False,
        export_yup=True,
        export_image_format="AUTO",
    )
    print(f"[export_pk] Đã xuất {out} ({os.path.getsize(out) // 1024} KB)")
    for obj in sorted(bpy.data.objects, key=lambda o: o.name):
        if obj.type == "MESH":
            print(f"[export_pk]   {obj.name}: floor={obj['floor']}")


main()
