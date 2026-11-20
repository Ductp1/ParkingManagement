import { useEffect, useMemo, useRef, useState } from 'react';
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { parkingApi } from '@/api/parkingApi';
import { getLotLayout } from './lotLayouts';
import { buildRoute, parkedCars, resolveTarget, toThree, type Point3 } from './routePlanner';
import { buildDynamicRoute, type DynamicFloorData, type DynamicRouteResult } from './astarGrid';
import { buildDynamicParkingScene, CELL_SIZE, FLOOR_HEIGHT, routePointToWorld } from './dynamicScene';

export interface ParkingRoute3DViewerProps {
  lotId: number;
  slotCode?: string | null;
  /** Tầng khi mã ô ở dạng rút gọn (ví dụ "Hầm B2"). */
  floorHint?: string | null;
  lotName?: string | null;
  onClose: () => void;
}

type LoadState = 'loading' | 'ready' | 'error' | 'no-webgl';

const ROUTE_COLOR = 0x2563eb;
const TARGET_COLOR = 0x10b981;
const CHEVRON_SPACING = 2.2;
const CHEVRON_SPEED = 2.2; // m/s

const v3 = (p: Point3) => new THREE.Vector3(...toThree(p));

/** Nhãn chữ luôn quay về phía camera. */
function makeLabel(text: string, color: string): THREE.Sprite {
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d')!;
  const font = '600 44px system-ui, "Segoe UI", sans-serif';
  ctx.font = font;
  const width = Math.ceil(ctx.measureText(text).width) + 48;
  canvas.width = width;
  canvas.height = 76;
  ctx.font = font;
  ctx.fillStyle = color;
  ctx.beginPath();
  ctx.roundRect(0, 0, width, 76, 20);
  ctx.fill();
  ctx.fillStyle = '#ffffff';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, 24, 40);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: texture, depthTest: false, transparent: true }));
  sprite.scale.set((width / 76) * 0.9, 0.9, 1);
  sprite.renderOrder = 10;
  return sprite;
}

/** Mũi tên phẳng, mũi hướng +Z (để Object3D.lookAt xoay theo hướng đi). */
function chevronGeometry(): THREE.BufferGeometry {
  const s = new THREE.Shape();
  s.moveTo(0, 0.42);
  s.lineTo(0.38, -0.18);
  s.lineTo(0.16, -0.18);
  s.lineTo(0, 0.08);
  s.lineTo(-0.16, -0.18);
  s.lineTo(-0.38, -0.18);
  s.closePath();
  const g = new THREE.ShapeGeometry(s);
  g.rotateX(Math.PI / 2);
  g.scale(1.6, 1.6, 1.6);
  return g;
}

/**
 * Xem 3D đường đi từ cổng vào tới ô đã đặt:
 * - Nếu bãi có mô hình GLB tĩnh (ví dụ Vincom lotId=1) và khớp ô -> dùng mô hình GLB.
 * - Ngược lại -> gọi API thật (hierarchy + layout 2D) và dựng mô hình 3D động + tìm đường bằng A*.
 */
export default function ParkingRoute3DViewer({ lotId, slotCode, floorHint, lotName, onClose }: ParkingRoute3DViewerProps) {
  const mountRef = useRef<HTMLDivElement>(null);
  const replayRef = useRef<() => void>(() => {});
  const [state, setState] = useState<LoadState>(() => (supportsWebGL() ? 'loading' : 'no-webgl'));
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Chế độ 1: Mô hình GLB tĩnh (nếu có)
  const staticLayout = getLotLayout(lotId);
  const staticTarget = useMemo(
    () => (staticLayout ? resolveTarget(staticLayout, slotCode, floorHint) : null),
    [staticLayout, slotCode, floorHint],
  );
  const isStaticMode = Boolean(staticLayout && staticTarget);

  const staticRoute = useMemo(
    () => (staticLayout && staticTarget ? buildRoute(staticLayout, staticTarget) : []),
    [staticLayout, staticTarget],
  );

  // Chế độ 2: Dữ liệu API thật + thuật toán A* cho mọi bãi khác
  const [dynamicFloors, setDynamicFloors] = useState<DynamicFloorData[] | null>(null);
  const [dynamicLotName, setDynamicLotName] = useState<string | null>(null);

  useEffect(() => {
    if (isStaticMode || state === 'no-webgl' || !lotId) return;

    let cancelled = false;
    setState('loading');
    setErrorMsg(null);
    setDynamicFloors(null);

    (async () => {
      try {
        const hierarchy = await parkingApi.getLotHierarchy(lotId);
        if (cancelled) return;
        setDynamicLotName(hierarchy.name);

        const allFloors = hierarchy.zones
          .flatMap((z) => z.floors)
          .sort((a, b) => a.level - b.level);

        if (allFloors.length === 0) {
          setErrorMsg('Bãi đỗ này chưa được cấu hình tầng hoặc sơ đồ 2D.');
          setState('error');
          return;
        }

        const layouts = await Promise.all(allFloors.map((fl) => parkingApi.getFloorLayout(fl.id)));
        if (cancelled) return;

        const floorData: DynamicFloorData[] = allFloors.map((fl, idx) => ({
          floorId: fl.id,
          floorName: fl.name,
          level: fl.level,
          layout: layouts[idx],
        }));

        setDynamicFloors(floorData);
      } catch {
        if (!cancelled) {
          setErrorMsg('Không tải được sơ đồ 2D của bãi từ hệ thống.');
          setState('error');
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [isStaticMode, lotId, state === 'no-webgl']);

  const dynamicRoute: DynamicRouteResult | null = useMemo(() => {
    if (isStaticMode || !dynamicFloors || !slotCode) return null;
    return buildDynamicRoute(dynamicFloors, slotCode, floorHint);
  }, [isStaticMode, dynamicFloors, slotCode, floorHint]);

  useEffect(() => {
    if (!isStaticMode && dynamicFloors && !dynamicRoute) {
      setErrorMsg(`Không tìm thấy ô "${slotCode ?? ''}" trên sơ đồ 2D của bãi.`);
      setState('error');
    }
  }, [isStaticMode, dynamicFloors, dynamicRoute, slotCode]);

  const routeWorldPoints = useMemo(() => {
    if (isStaticMode) {
      return staticRoute.map(v3);
    }
    if (dynamicFloors && dynamicRoute) {
      return dynamicRoute.points.map((pt) => routePointToWorld(pt, dynamicFloors));
    }
    return [];
  }, [isStaticMode, staticRoute, dynamicFloors, dynamicRoute]);

  const routeLength = useMemo(
    () =>
      routeWorldPoints.reduce(
        (sum, p, i) => (i === 0 ? 0 : sum + p.distanceTo(routeWorldPoints[i - 1])),
        0,
      ),
    [routeWorldPoints],
  );

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  useEffect(() => {
    const mount = mountRef.current;
    if (!mount || state === 'no-webgl' || routeWorldPoints.length < 2) return;
    if (isStaticMode && (!staticLayout || !staticTarget)) return;
    if (!isStaticMode && (!dynamicFloors || !dynamicRoute)) return;

    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({ antialias: true });
    } catch {
      return;
    }

    const reducedMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    mount.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0xe8edf3);
    scene.add(new THREE.HemisphereLight(0xffffff, 0x8090a0, 2.2));
    const sun = new THREE.DirectionalLight(0xffffff, 1.6);
    sun.position.set(16, 36, 22);
    scene.add(sun);

    const end = routeWorldPoints[routeWorldPoints.length - 1];
    const start = routeWorldPoints[0];
    const camera = new THREE.PerspectiveCamera(45, 1, 0.1, 500);

    if (isStaticMode && staticTarget) {
      const elevation = staticTarget.floor.elevation;
      const focus = staticTarget.slot ? v3([...staticTarget.slot.center, elevation]) : new THREE.Vector3(0, elevation, 0);
      const far = staticTarget.floor.modelFloor > 1 ? 1.25 : 1;
      camera.position.set(focus.x * 0.4 + 4, elevation + 24 * far, focus.z * 0.4 + 26 * far);
    } else if (dynamicFloors && dynamicRoute) {
      const targetFl = dynamicFloors[dynamicRoute.targetFloorIndex];
      const span = Math.max(targetFl.layout.columns, targetFl.layout.rows) * CELL_SIZE;
      const elev = dynamicRoute.targetFloorIndex * FLOOR_HEIGHT;
      const dist = Math.max(22, span * 0.95);
      camera.position.set(end.x * 0.35 + dist * 0.35, elev + dist * 0.72, end.z * 0.35 + dist * 0.8);
    }

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.target.set(
      (start.x + end.x) * 0.25,
      end.y,
      (start.z + end.z) * 0.25,
    );
    controls.enableDamping = true;
    controls.maxPolarAngle = Math.PI * 0.47;
    controls.minDistance = 6;
    controls.maxDistance = 120;

    // ---- Đường đi ----
    const disposables: { dispose: () => void }[] = [];
    const curve = new THREE.CatmullRomCurve3(routeWorldPoints, false, 'catmullrom', 0.08);
    const tubeGeo = new THREE.TubeGeometry(curve, Math.max(64, routeWorldPoints.length * 12), 0.12, 8, false);
    const ghostMat = new THREE.MeshBasicMaterial({
      color: ROUTE_COLOR,
      transparent: true,
      opacity: 0.28,
      depthTest: false,
      depthWrite: false,
    });
    const solidMat = new THREE.MeshBasicMaterial({ color: ROUTE_COLOR, transparent: true, opacity: 0.78 });
    const ghost = new THREE.Mesh(tubeGeo, ghostMat);
    ghost.renderOrder = 5;
    scene.add(ghost, new THREE.Mesh(tubeGeo, solidMat));
    disposables.push(tubeGeo, ghostMat, solidMat);

    const chevronGeo = chevronGeometry();
    const chevronMat = new THREE.MeshBasicMaterial({ color: ROUTE_COLOR, side: THREE.DoubleSide });
    disposables.push(chevronGeo, chevronMat);
    const length = Math.max(0.1, curve.getLength());
    const chevrons = Array.from({ length: Math.max(1, Math.floor(length / CHEVRON_SPACING)) }, () => {
      const m = new THREE.Mesh(chevronGeo, chevronMat);
      scene.add(m);
      return m;
    });
    const placeChevrons = (offset: number, revealUpTo: number) => {
      chevrons.forEach((m, i) => {
        const d = (i * CHEVRON_SPACING + offset) % length;
        m.visible = d <= revealUpTo;
        const t = d / length;
        const p = curve.getPointAt(t);
        m.position.copy(p).y += 0.04;
        m.lookAt(p.clone().add(curve.getTangentAt(t)));
      });
    };

    // ---- Nhãn cổng vào & ô đích ----
    const gateLabel = makeLabel('Cổng vào', '#334155');
    gateLabel.position.copy(start).y += 1.8;
    scene.add(gateLabel);

    const pulseMat = new THREE.MeshBasicMaterial({
      color: TARGET_COLOR,
      transparent: true,
      opacity: 0.45,
      depthWrite: false,
    });
    const pulse = new THREE.Mesh(new THREE.BoxGeometry(2.3, 0.06, 1.9), pulseMat);
    pulse.position.copy(end);
    disposables.push(pulse.geometry, pulseMat);

    const pin = new THREE.Mesh(
      new THREE.ConeGeometry(0.45, 1.1, 24),
      new THREE.MeshStandardMaterial({ color: TARGET_COLOR }),
    );
    pin.rotation.x = Math.PI;
    pin.position.copy(end).y += 2.2;
    disposables.push(pin.geometry, pin.material as THREE.Material);

    const targetLabelText = isStaticMode && staticTarget
      ? staticTarget.slot
        ? `Ô ${staticTarget.code}`
        : staticTarget.floor.label
      : dynamicRoute?.targetCell
      ? `Ô ${slotCode?.trim().toUpperCase()}`
      : dynamicFloors?.[dynamicRoute?.targetFloorIndex ?? 0]?.floorName ?? 'Đích';

    const targetLabel = makeLabel(targetLabelText, '#059669');
    targetLabel.position.copy(end).y += 3.4;
    scene.add(pulse, pin, targetLabel);
    if (isStaticMode && staticTarget && !staticTarget.slot) pulse.visible = false;
    disposables.push(gateLabel.material.map!, gateLabel.material, targetLabel.material.map!, targetLabel.material);

    // ---- Dựng mô hình bãi (GLB tĩnh hoặc 3D động từ API) ----
    let model: THREE.Object3D | null = null;
    let cancelled = false;

    if (isStaticMode && staticLayout && staticTarget) {
      const cars = parkedCars(staticLayout, staticTarget);
      if (cars.length) {
        const bodyGeo = new THREE.BoxGeometry(2.4, 0.5, 1.45);
        const cabinGeo = new THREE.BoxGeometry(1.3, 0.42, 1.25);
        const wheelGeo = new THREE.CylinderGeometry(0.26, 0.26, 0.18, 12).rotateX(Math.PI / 2);
        const bodyMat = new THREE.MeshStandardMaterial({ roughness: 0.45, metalness: 0.3 });
        const cabinMat = new THREE.MeshStandardMaterial({ color: 0x1e293b, roughness: 0.15, metalness: 0.6 });
        const wheelMat = new THREE.MeshStandardMaterial({ color: 0x111111, roughness: 0.9 });
        const bodies = new THREE.InstancedMesh(bodyGeo, bodyMat, cars.length);
        const cabins = new THREE.InstancedMesh(cabinGeo, cabinMat, cars.length);
        const wheels = new THREE.InstancedMesh(wheelGeo, wheelMat, cars.length * 4);
        const m = new THREE.Matrix4();
        const color = new THREE.Color();
        cars.forEach((car, i) => {
          const p = v3(car.position);
          m.makeTranslation(p.x, p.y + 0.5, p.z);
          bodies.setMatrixAt(i, m);
          bodies.setColorAt(i, color.setHex(car.color));
          m.makeTranslation(p.x - 0.1, p.y + 0.96, p.z);
          cabins.setMatrixAt(i, m);
          [
            [0.8, 0.72],
            [0.8, -0.72],
            [-0.8, 0.72],
            [-0.8, -0.72],
          ].forEach(([dx, dz], w) => {
            m.makeTranslation(p.x + dx, p.y + 0.26, p.z + dz);
            wheels.setMatrixAt(i * 4 + w, m);
          });
        });
        scene.add(bodies, cabins, wheels);
        disposables.push(bodyGeo, cabinGeo, wheelGeo, bodyMat, cabinMat, wheelMat, bodies, cabins, wheels);
      }

      new GLTFLoader().load(
        staticLayout.modelUrl,
        (gltf) => {
          if (cancelled) {
            gltf.scene.traverse((o) => o instanceof THREE.Mesh && o.geometry.dispose());
            return;
          }
          model = gltf.scene;
          model.traverse((o) => {
            const floor = Number(o.userData?.floor ?? 1);
            if (o.userData?.floor !== undefined && floor > staticTarget.floor.modelFloor) o.visible = false;
          });
          scene.add(model);
          setState('ready');
        },
        undefined,
        () => !cancelled && setState('error'),
      );
    } else if (dynamicFloors && dynamicRoute) {
      const dynDisposables = buildDynamicParkingScene(scene, dynamicFloors, dynamicRoute, slotCode ?? '');
      disposables.push(...dynDisposables);
      setState('ready');
    }

    // ---- Vòng lặp render ----
    const resize = () => {
      const { clientWidth: w, clientHeight: h } = mount;
      renderer.setSize(w, h);
      camera.aspect = w / Math.max(h, 1);
      const wideTan = Math.tan(THREE.MathUtils.degToRad(22.5)) * 1.3;
      camera.fov = THREE.MathUtils.clamp(THREE.MathUtils.radToDeg(2 * Math.atan(wideTan / camera.aspect)), 45, 78);
      camera.updateProjectionMatrix();
    };
    const observer = new ResizeObserver(resize);
    observer.observe(mount);
    resize();

    const clock = new THREE.Clock();
    let revealStart = clock.getElapsedTime();
    replayRef.current = () => {
      revealStart = clock.getElapsedTime();
    };
    let frame = 0;
    const tick = () => {
      const t = clock.getElapsedTime();
      const reveal = reducedMotion ? length : Math.min(length, (t - revealStart) * CHEVRON_SPEED * 4);
      placeChevrons(reducedMotion ? 0 : (t * CHEVRON_SPEED) % CHEVRON_SPACING, reveal);
      const k = 0.5 + 0.5 * Math.sin(t * 4);
      pulseMat.opacity = reducedMotion ? 0.45 : 0.25 + 0.4 * k;
      pin.position.y = end.y + 2.2 + (reducedMotion ? 0 : 0.25 * k);
      controls.update();
      renderer.render(scene, camera);
      frame = requestAnimationFrame(tick);
    };
    tick();

    return () => {
      cancelled = true;
      cancelAnimationFrame(frame);
      observer.disconnect();
      controls.dispose();
      disposables.forEach((d) => d.dispose());
      model?.traverse((o) => {
        if (o instanceof THREE.Mesh) {
          o.geometry.dispose();
          (Array.isArray(o.material) ? o.material : [o.material]).forEach(
            (m: THREE.Material & { map?: THREE.Texture | null }) => {
              m.map?.dispose();
              m.dispose();
            },
          );
        }
      });
      renderer.dispose();
      if (mount.contains(renderer.domElement)) {
        mount.removeChild(renderer.domElement);
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isStaticMode, staticLayout, staticTarget, dynamicFloors, dynamicRoute, routeWorldPoints]);

  const notOnModel = isStaticMode ? staticTarget && !staticTarget.slot : dynamicRoute?.usedFallback;
  const displayLotName = lotName || (isStaticMode ? staticLayout?.name : dynamicLotName) || 'Bãi đỗ xe';
  const displayFloorLabel = isStaticMode
    ? staticTarget?.floor.label
    : dynamicFloors && dynamicRoute
    ? dynamicFloors[dynamicRoute.targetFloorIndex]?.floorName
    : floorHint;
  const displaySlotCode = isStaticMode ? staticTarget?.code : slotCode?.trim().toUpperCase();

  return (
    <div
      className="fixed inset-0 z-[60] bg-slate-900/70 backdrop-blur-xs flex items-center justify-center p-2 sm:p-4"
      role="dialog"
      aria-modal="true"
      aria-label="Đường đi 3D tới ô đỗ"
    >
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-5xl h-[88vh] flex flex-col overflow-hidden border border-slate-200">
        <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-3 border-b border-slate-100">
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <h3 className="text-base font-bold text-slate-900 truncate">Đường đi tới ô đỗ</h3>
              {!isStaticMode && state === 'ready' && (
                <span className="px-2 py-0.5 rounded-full bg-blue-50 text-blue-700 text-[10px] font-bold border border-blue-200">
                  3D Dynamic (A*)
                </span>
              )}
            </div>
            <p className="text-xs text-slate-500 truncate">
              {displayLotName}
              {displayFloorLabel && <> • {displayFloorLabel}</>}
              {displaySlotCode && (
                <>
                  {' '}
                  • <strong className="text-emerald-600">Ô {displaySlotCode}</strong>
                </>
              )}
              {routeWorldPoints.length > 1 && <> • ~{Math.round(routeLength)} m từ cổng vào</>}
            </p>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            {state === 'ready' && (
              <button
                type="button"
                onClick={() => replayRef.current()}
                className="hidden sm:flex items-center gap-1 px-3 py-1.5 rounded-lg border border-slate-200 text-xs font-semibold text-slate-700 hover:bg-slate-50 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">replay</span>Xem lại
              </button>
            )}
            <button
              type="button"
              onClick={onClose}
              aria-label="Đóng"
              className="text-slate-400 hover:text-slate-700 p-1 rounded-full cursor-pointer"
            >
              <span className="material-symbols-outlined text-[22px]">close</span>
            </button>
          </div>
        </div>

        <div className="relative flex-1 min-h-0">
          <div ref={mountRef} className="absolute inset-0" />
          {state === 'loading' && (
            <Overlay
              icon="progress_activity"
              text={isStaticMode ? 'Đang tải mô hình bãi đỗ…' : 'Đang dựng mô hình 3D & tìm đường tối ưu (A*)…'}
              spin
            />
          )}
          {state === 'error' && (
            <Overlay icon="error" text={errorMsg || 'Không tải được mô hình 3D. Vui lòng thử lại sau.'} />
          )}
          {state === 'no-webgl' && (
            <Overlay icon="desktop_access_disabled" text="Trình duyệt không hỗ trợ WebGL để hiển thị 3D." />
          )}
          {state === 'ready' && notOnModel && (
            <div className="absolute top-3 left-3 right-3 sm:right-auto max-w-sm px-3 py-2 rounded-xl bg-amber-50 border border-amber-200 text-amber-800 text-xs font-semibold">
              {isStaticMode
                ? `Ô ${displaySlotCode} chưa được vẽ trên mô hình 3D – đường đi dẫn tới ${displayFloorLabel}.`
                : 'Sơ đồ 2D chưa nối thông hoàn toàn làn đường – đang hiển thị hướng dẫn trực tiếp tới ô.'}
            </div>
          )}
          {state === 'ready' && (
            <div className="absolute bottom-3 right-3 flex flex-wrap gap-x-4 gap-y-1 px-3 py-2 rounded-xl bg-white/90 border border-slate-200 text-[11px] text-slate-600">
              <span className="flex items-center gap-1.5">
                <i className="w-3 h-1 rounded bg-blue-600 inline-block" />
                Đường đi
              </span>
              <span className="flex items-center gap-1.5">
                <i className="w-3 h-3 rounded bg-emerald-500 inline-block" />
                Ô đã đặt
              </span>
              <span>Kéo để xoay • Cuộn để phóng to</span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function supportsWebGL(): boolean {
  try {
    const canvas = document.createElement('canvas');
    return !!(canvas.getContext('webgl2') || canvas.getContext('webgl'));
  } catch {
    return false;
  }
}

function Overlay({ icon, text, spin }: { icon: string; text: string; spin?: boolean }) {
  return (
    <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 text-slate-500 text-sm">
      <span className={`material-symbols-outlined text-[32px] ${spin ? 'animate-spin' : ''}`}>{icon}</span>
      {text}
    </div>
  );
}
