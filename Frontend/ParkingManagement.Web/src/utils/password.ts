export function passwordError(password: string): string | undefined {
  if (password.length < 8 || new TextEncoder().encode(password).length > 72
    || !/\p{L}/u.test(password) || !/\p{Nd}/u.test(password))
    return 'Mật khẩu cần có chữ và số, ít nhất 8 ký tự và tối đa 72 byte UTF-8.';
}
