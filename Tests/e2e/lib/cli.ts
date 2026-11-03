/** Các project được chọn qua --project=x hoặc --project x (rỗng = chạy tất cả). */
export function selectedProjects(argv = process.argv): string[] {
  return argv.flatMap((a, i) =>
    a.startsWith('--project=') ? [a.slice('--project='.length)] : a === '--project' ? [argv[i + 1]] : []);
}

export function runsProject(name: string): boolean {
  const selected = selectedProjects();
  return selected.length === 0 || selected.includes(name);
}
