import { applyLook, defaultLook, rememberLook, rememberedLook } from './look';

class MemoryStorage implements Storage {
  private readonly items = new Map<string, string>();
  get length(): number {
    return this.items.size;
  }
  clear(): void {
    this.items.clear();
  }
  getItem(key: string): string | null {
    return this.items.get(key) ?? null;
  }
  key(index: number): string | null {
    return [...this.items.keys()][index] ?? null;
  }
  removeItem(key: string): void {
    this.items.delete(key);
  }
  setItem(key: string, value: string): void {
    this.items.set(key, value);
  }
}

describe('look', () => {
  it('remembers and reads back the two switches', () => {
    const storage = new MemoryStorage();
    rememberLook(storage, { theme: 'dark', density: 'compact' });
    expect(rememberedLook(storage)).toEqual({ theme: 'dark', density: 'compact' });
  });

  it('no storage, nothing stored, or nonsense: the default look', () => {
    expect(rememberedLook(null)).toEqual(defaultLook);
    expect(rememberedLook(new MemoryStorage())).toEqual(defaultLook);
    const storage = new MemoryStorage();
    storage.setItem('harmony.look', '{"theme":"purple","density":7}');
    expect(rememberedLook(storage)).toEqual(defaultLook);
    storage.setItem('harmony.look', 'not json');
    expect(rememberedLook(storage)).toEqual(defaultLook);
  });

  it('applies the switches as attributes on <html>, absent for the defaults', () => {
    const root = document.createElement('html');
    applyLook(root, { theme: 'dark', density: 'comfortable' });
    expect(root.getAttribute('data-theme')).toBe('dark');
    expect(root.getAttribute('data-density')).toBe('comfortable');
    applyLook(root, defaultLook);
    expect(root.hasAttribute('data-theme')).toBe(false);
    expect(root.hasAttribute('data-density')).toBe(false);
  });
});
