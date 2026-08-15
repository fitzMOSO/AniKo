import { StatTile } from './StatTile'
import { useOverviewStats } from './useOverviewStats'

/**
 * The stat band at the top of Overview. Owns the grid; the tile owns itself.
 * Four across on desktop, two on tablet, stacked on a phone — the mockup's
 * four-up row is unreadable below ~640px.
 */
export function StatTilesRow() {
  const { stats } = useOverviewStats()

  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4">
      {stats.map((stat) => (
        <StatTile key={stat.key} stat={stat} />
      ))}
    </div>
  )
}
