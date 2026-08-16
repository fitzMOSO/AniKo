import { renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RECENT_ORDERS } from './fixtures'
import { useRecentOrders } from './useRecentOrders'

describe('useRecentOrders', () => {
  it('returns exactly the number of orders asked for', () => {
    const { result } = renderHook(() => useRecentOrders(3))
    expect(result.current.orders).toHaveLength(3)
  })

  it('takes them from the top of the list, in the order they were placed', () => {
    const { result } = renderHook(() => useRecentOrders(2))
    expect(result.current.orders.map((o) => o.id)).toEqual([
      RECENT_ORDERS[0].id,
      RECENT_ORDERS[1].id,
    ])
  })

  /*
   * A limit larger than the data is the everyday case for a new account, not
   * an error state. It must return what exists — never pad, never throw.
   */
  it('returns everything, without padding, when the limit exceeds the data', () => {
    const { result } = renderHook(() => useRecentOrders(RECENT_ORDERS.length + 50))
    expect(result.current.orders).toHaveLength(RECENT_ORDERS.length)
    expect(result.current.orders.every(Boolean)).toBe(true)
  })

  it('treats a limit of zero as an empty panel rather than an unlimited one', () => {
    const { result } = renderHook(() => useRecentOrders(0))
    expect(result.current.orders).toEqual([])
  })

  it('never hands back a slice that aliases the fixture array', () => {
    // Callers sort and filter results; if this were the fixture itself, one
    // caller could reorder every other consumer's data for the session.
    const { result } = renderHook(() => useRecentOrders(RECENT_ORDERS.length))
    expect(result.current.orders).not.toBe(RECENT_ORDERS)
  })

  it('reslices when the limit changes', () => {
    const { result, rerender } = renderHook(({ limit }) => useRecentOrders(limit), {
      initialProps: { limit: 1 },
    })
    expect(result.current.orders).toHaveLength(1)
    rerender({ limit: 4 })
    expect(result.current.orders).toHaveLength(4)
  })

  it('reports raw values, leaving formatting to the view', () => {
    const { result } = renderHook(() => useRecentOrders(5))
    for (const order of result.current.orders) {
      expect(typeof order.quantityKg).toBe('number')
      // ISO, not a rendered date: the row formats it against the active locale.
      expect(order.estimatedDelivery).toMatch(/^\d{4}-\d{2}-\d{2}$/)
    }
  })
})
