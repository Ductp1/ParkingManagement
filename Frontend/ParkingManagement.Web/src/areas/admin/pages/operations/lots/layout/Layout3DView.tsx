import React, { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';
import type { LayoutCellDto } from '@/types/ownerLayout';
import { AdminButton } from '../../../../components/AdminButton';
import { StateBlock } from '../../../../components/StateBlock';
import { humanizeEnum } from '../../../../utils/enumLabels';
import { buildLayoutGroup, disposeGroup, type SceneBounds } from './layoutScene';
import type { FloorModel } from './layoutModel';

interface Layout3DViewProps {
  floors: FloorModel[];
  activeFloorId: number;
  /** Màu theo trạng thái ô đỗ (bản đang áp dụng) hay chỉ theo loại ô (đang thiết kế). */
  showState: boolean;
}

const VIEWS = { front: [0, 0.62, 0.72], isometric: [0.95, 1.07, 0.95], top: [0, 1.15, 0.001] } as const;
type ViewName = keyof typeof VIEWS;
const SKY = '#dbe6f3';

interface Stage {
  scene: THREE.Scene;
  camera: THREE.PerspectiveCamera;
  controls: OrbitControls;
  sun: THREE.DirectionalLight;
  ground: THREE.Mesh;
  canvas: HTMLCanvasElement;
  schedule: () => void;
  frame: (view: ViewName) => void;
  bounds: SceneBounds | null;
  group: THREE.Group | null;
}

interface Hover {
  cell: LayoutCellDto;
  left: number;
  top: number;
}

/** Mô hình 3D của bãi dựng từ sơ đồ: kéo để xoay, cuộn để phóng to, chuột phải để di chuyển. Tải lazy (three.js). */
const Layout3DView: React.FC<Layout3DViewProps> = ({ floors, activeFloorId, showState }) => {
  const hostRef = useRef<HTMLDivElement>(null);
  const stageRef = useRef<Stage | null>(null);
  const framedFloor = useRef<number | null>(null);
  const [failed, setFailed] = useState(false);
  const [hover, setHover] = useState<Hover | null>(null);

  // Một renderer cho cả vòng đời; sơ đồ đổi chỉ dựng lại khối (effect dưới), giữ nguyên góc nhìn.
  useEffect(() => {
    const host = hostRef.current;
    if (!host) return;
    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({ antialias: true });
    } catch {
      // Chỉ biết trình duyệt không vẽ được 3D khi tạo renderer trong effect.
      // oxlint-disable-next-line react/set-state-in-effect
      setFailed(true);
      return;
    }
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    host.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(SKY);
    const pmrem = new THREE.PMREMGenerator(renderer);
    const room = new RoomEnvironment();
    const environment = pmrem.fromScene(room, 0.04);
    scene.environment = environment.texture;
    scene.environmentIntensity = 0.5;
    scene.add(new THREE.HemisphereLight('#eaf2ff', '#8a8f98', 0.9));
    const sun = new THREE.DirectionalLight('#fff4e0', 2.2);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    sun.shadow.bias = -0.0004;
    scene.add(sun, sun.target);
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), new THREE.MeshStandardMaterial({ color: '#c9d3c4', roughness: 1 }));
    ground.rotation.x = -Math.PI / 2;
    ground.receiveShadow = true;
    scene.add(ground);

    const camera = new THREE.PerspectiveCamera(40, 1, 0.5, 4000);
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.maxPolarAngle = Math.PI / 2.1;
    controls.enableDamping = true;

    // Chỉ vẽ khi có thay đổi: đang xoay (kể cả quán tính) thì update() báo "change" và xin khung hình tiếp.
    let pending = 0;
    const schedule = () => {
      if (pending) return;
      pending = requestAnimationFrame(() => {
        pending = 0;
        controls.update();
        renderer.render(scene, camera);
      });
    };
    controls.addEventListener('change', schedule);

    const stage: Stage = {
      scene,
      camera,
      controls,
      sun,
      ground,
      canvas: renderer.domElement,
      schedule,
      bounds: null,
      group: null,
      frame: (view) => {
        if (!stage.bounds) return;
        const { center, size } = stage.bounds;
        // Khung hẹp thì lùi xa thêm cho vừa chiều ngang.
        const distance = size * Math.max(1, 1.5 / camera.aspect);
        const [x, y, z] = VIEWS[view];
        controls.target.copy(center);
        camera.position.set(center.x + x * distance, center.y + y * distance, center.z + z * distance);
        controls.update();
        schedule();
      },
    };
    stageRef.current = stage;

    const resize = () => {
      const width = host.clientWidth || 600;
      const height = host.clientHeight || 480;
      renderer.setSize(width, height);
      camera.aspect = width / height;
      camera.updateProjectionMatrix();
      schedule();
    };
    const observer = new ResizeObserver(resize);
    observer.observe(host);
    resize();

    return () => {
      stageRef.current = null;
      framedFloor.current = null;
      cancelAnimationFrame(pending);
      observer.disconnect();
      controls.dispose();
      ground.geometry.dispose();
      (ground.material as THREE.Material).dispose();
      sun.shadow.dispose();
      environment.dispose();
      room.dispose();
      pmrem.dispose();
      renderer.dispose();
      renderer.forceContextLoss();
      renderer.domElement.remove();
    };
  }, []);

  useEffect(() => {
    const stage = stageRef.current;
    if (!stage) return;
    const { group, bounds } = buildLayoutGroup(floors, activeFloorId, showState);
    const { center, size } = bounds;
    stage.scene.add(group);
    stage.group = group;
    stage.bounds = bounds;
    stage.scene.fog = new THREE.Fog(SKY, size * 2.2, size * 6);
    stage.ground.scale.set(size * 12, size * 12, 1);
    stage.ground.position.set(center.x, -0.34, center.z);
    stage.sun.position.set(center.x - size * 0.5, center.y + size, center.z + size * 0.35);
    stage.sun.target.position.copy(center);
    const shadow = stage.sun.shadow.camera;
    shadow.left = shadow.bottom = -size * 0.85;
    shadow.right = shadow.top = size * 0.85;
    shadow.near = 0.5;
    shadow.far = size * 3;
    shadow.updateProjectionMatrix();
    if (framedFloor.current !== activeFloorId) {
      framedFloor.current = activeFloorId;
      stage.frame('front');
    }
    stage.schedule();
    return () => {
      stage.scene.remove(group);
      disposeGroup(group);
      stage.group = null;
    };
  }, [floors, activeFloorId, showState]);

  const onPointerMove = (e: React.PointerEvent<HTMLDivElement>) => {
    const stage = stageRef.current;
    if (!stage?.group || e.buttons) return setHover(null);
    const rect = stage.canvas.getBoundingClientRect();
    const ray = new THREE.Raycaster();
    ray.setFromCamera(
      new THREE.Vector2(((e.clientX - rect.left) / rect.width) * 2 - 1, -((e.clientY - rect.top) / rect.height) * 2 + 1),
      stage.camera
    );
    const pads = stage.group.getObjectByName(`floor-${activeFloorId}`)?.getObjectByName('slots');
    const hit = pads ? ray.intersectObject(pads)[0] : undefined;
    const cell = hit?.instanceId === undefined ? undefined : (pads!.userData.cells as LayoutCellDto[])[hit.instanceId];
    setHover(cell ? { cell, left: e.clientX - rect.left, top: e.clientY - rect.top } : null);
  };

  if (failed) {
    return <StateBlock type="error" title="3D view unavailable" message="This browser cannot draw 3D graphics (WebGL). Use the 2D view." />;
  }
  return (
    <div className="flex flex-col gap-2">
      <div role="group" aria-label="Camera" className="flex flex-wrap items-center gap-1">
        {(Object.keys(VIEWS) as ViewName[]).map((view) => (
          <AdminButton key={view} variant="secondary" onClick={() => stageRef.current?.frame(view)}>
            {view === 'front' ? 'Reset view' : humanizeEnum(view.charAt(0).toUpperCase() + view.slice(1))}
          </AdminButton>
        ))}
        <span className="adm-type-body-small ml-2 text-(--admin-text-secondary)">Drag to rotate, scroll to zoom, hover a slot for details.</span>
      </div>
      <div className="relative">
        <div
          ref={hostRef}
          role="img"
          aria-label="3D model of the parking lot. The 2D view lists the same slots."
          onPointerMove={onPointerMove}
          onPointerLeave={() => setHover(null)}
          className="h-[480px] w-full overflow-hidden border border-(--admin-border-default)"
        />
        {hover && (
          <div
            className="pointer-events-none absolute z-10 flex flex-col rounded-[4px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2.5 py-1.5 shadow-[0_4px_12px_rgb(0_0_0/0.18)]"
            style={{ left: hover.left + 14, top: hover.top + 14 }}
          >
            <span className="adm-type-body-strong text-(--admin-text-primary)">{hover.cell.code}</span>
            <span className="adm-type-body-small text-(--admin-text-secondary)">
              {[
                showState && hover.cell.state ? humanizeEnum(hover.cell.state) : null,
                hover.cell.slotType ? humanizeEnum(hover.cell.slotType) : null,
                hover.cell.maxVehicleType ? `up to ${humanizeEnum(hover.cell.maxVehicleType)}` : null,
              ]
                .filter(Boolean)
                .join(' · ')}
            </span>
          </div>
        )}
      </div>
    </div>
  );
};

export default Layout3DView;
