import en from './en.json'
import fil from './fil.json'

function keysOf(obj: Record<string, unknown>): string[] {
  return Object.keys(obj).sort()
}

test('en and fil catalogues have identical key sets', () => {
  expect(keysOf(fil)).toEqual(keysOf(en))
})

test('no catalogue value is an empty string', () => {
  for (const [key, value] of [...Object.entries(en), ...Object.entries(fil)]) {
    expect(value, `empty value for "${key}"`).not.toBe('')
  }
})
