import type { AdminIconAsset } from '../navigation';
import search from '../assets/icons/icon-search.svg';
import chevronDown from '../assets/icons/icon-chevron-down.svg';
import chevronLeft from '../assets/icons/icon-chevron-left.svg';
import chevronRight from '../assets/icons/icon-chevron-right.svg';
import calendar from '../assets/icons/icon-calendar.svg';
import upload from '../assets/icons/icon-upload.svg';
import x from '../assets/icons/icon-x.svg';
import check from '../assets/icons/icon-check.svg';
import alert from '../assets/icons/icon-alert.svg';
import info from '../assets/icons/icon-info.svg';
import download from '../assets/icons/icon-download.svg';
import plus from '../assets/icons/icon-plus.svg';
import lock from '../assets/icons/icon-lock.svg';
import unlock from '../assets/icons/icon-unlock.svg';
import refresh from '../assets/icons/icon-refresh.svg';
import more from '../assets/icons/icon-more.svg';
import external from '../assets/icons/icon-external.svg';
import eye from '../assets/icons/icon-eye.svg';
import eyeOff from '../assets/icons/icon-eye-off.svg';
import clock from '../assets/icons/icon-clock.svg';
import file from '../assets/icons/icon-file.svg';
import user from '../assets/icons/icon-user.svg';
import logOut from '../assets/icons/icon-log-out.svg';
import inbox from '../assets/icons/icon-inbox.svg';
import ban from '../assets/icons/icon-ban.svg';
import edit from '../assets/icons/icon-edit.svg';
import trash from '../assets/icons/icon-trash.svg';
import bell from '../assets/icons/icon-bell.svg';
import send from '../assets/icons/icon-send.svg';

const icon16 = (src: string): AdminIconAsset => ({ src, width: 16, height: 16 });

/** Bộ icon 16×16 xuất từ Figma (Design System › Icon/*). */
export const ADMIN_ICONS = {
  search: icon16(search),
  chevronDown: icon16(chevronDown),
  chevronLeft: icon16(chevronLeft),
  chevronRight: icon16(chevronRight),
  calendar: icon16(calendar),
  upload: icon16(upload),
  x: icon16(x),
  check: icon16(check),
  alert: icon16(alert),
  info: icon16(info),
  download: icon16(download),
  plus: icon16(plus),
  lock: icon16(lock),
  unlock: icon16(unlock),
  refresh: icon16(refresh),
  more: icon16(more),
  external: icon16(external),
  eye: icon16(eye),
  eyeOff: icon16(eyeOff),
  clock: icon16(clock),
  file: icon16(file),
  user: icon16(user),
  logOut: icon16(logOut),
  inbox: icon16(inbox),
  ban: icon16(ban),
  edit: icon16(edit),
  trash: icon16(trash),
  bell: icon16(bell),
  send: icon16(send),
} satisfies Record<string, AdminIconAsset>;

export type AdminIconName = keyof typeof ADMIN_ICONS;

/** Phóng icon 16px lên kích thước khác (State block dùng 32px). */
export const scaleIcon = (name: AdminIconName, size: number): AdminIconAsset => ({ ...ADMIN_ICONS[name], width: size, height: size });
