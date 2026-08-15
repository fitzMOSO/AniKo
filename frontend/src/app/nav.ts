import {
  Home,
  Store,
  Package,
  MessageSquare,
  Truck,
  CreditCard,
  type LucideIcon,
} from 'lucide-react'

export interface NavItem {
  key: string
  labelKey: string
  to: string
  icon: LucideIcon
  badge?: number
}

export const NAV_ITEMS: NavItem[] = [
  { key: 'overview', labelKey: 'nav.overview', to: '/overview', icon: Home },
  { key: 'marketplace', labelKey: 'nav.marketplace', to: '/marketplace', icon: Store },
  { key: 'orders', labelKey: 'nav.orders', to: '/orders', icon: Package },
  { key: 'messages', labelKey: 'nav.messages', to: '/messages', icon: MessageSquare, badge: 3 },
  { key: 'logistics', labelKey: 'nav.logistics', to: '/logistics', icon: Truck },
  { key: 'payments', labelKey: 'nav.payments', to: '/payments', icon: CreditCard },
]
