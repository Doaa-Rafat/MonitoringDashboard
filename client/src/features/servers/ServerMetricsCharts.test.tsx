import type { ReactNode } from 'react'
import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { ServerMetricsCharts } from './ServerMetricsCharts'

const mocks = vi.hoisted(() => ({
  useQuery: vi.fn(),
  useQueryClient: vi.fn(),
  metrics: vi.fn(),
}))

vi.mock('@tanstack/react-query', () => ({
  useQuery: mocks.useQuery,
  useQueryClient: mocks.useQueryClient,
}))

vi.mock('./api', () => ({ serversApi: { metrics: mocks.metrics } }))
vi.mock('@/realtime/realtimeContext', () => ({
  useHubEvent: vi.fn(),
  useHubSubscription: vi.fn(),
}))

vi.mock('recharts', () => ({
  CartesianGrid: () => null,
  Legend: () => <span>Legend</span>,
  Line: ({ dataKey, name }: { dataKey: string; name: string }) =>
    <span data-testid={`series-${dataKey}`}>{name}</span>,
  LineChart: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  ResponsiveContainer: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}))

describe('ServerMetricsCharts network throughput', () => {
  beforeEach(() => {
    mocks.useQuery.mockReturnValue({
      data: [{
        id: 1, serverId: 4, cpuUsage: 20, memoryUsage: 30, diskUsage: 40, responseTime: 50,
        networkInKbps: 1500, networkOutKbps: 750, status: 'Up', timestamp: '2026-01-01T00:00:00Z',
      }],
      isPending: false,
      isError: false,
    })
    mocks.useQueryClient.mockReturnValue({ setQueryData: vi.fn() })
  })

  it('renders separate inbound and outbound chart series', () => {
    render(<ServerMetricsCharts serverId={4} />)

    expect(screen.getByText('Network throughput')).toBeInTheDocument()
    expect(screen.getByTestId('series-networkInKbps')).toHaveTextContent('Inbound')
    expect(screen.getByTestId('series-networkOutKbps')).toHaveTextContent('Outbound')
  })

  it('explains when legacy history has no network samples', () => {
    mocks.useQuery.mockReturnValue({
      data: [{
        id: 1, serverId: 4, cpuUsage: 20, memoryUsage: 30, diskUsage: 40, responseTime: 50,
        networkInKbps: null, networkOutKbps: null, status: 'Up', timestamp: '2026-01-01T00:00:00Z',
      }],
      isPending: false,
      isError: false,
    })

    render(<ServerMetricsCharts serverId={4} from="2026-01-01T00:00:00Z" />)

    expect(screen.getByRole('status')).toHaveTextContent('Earlier network history is unavailable')
    expect(screen.queryByTestId('series-networkInKbps')).not.toBeInTheDocument()
  })
})
