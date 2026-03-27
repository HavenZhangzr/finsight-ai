import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Avatar,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Divider,
  IconButton,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
  Slide,
} from '@mui/material';
import SendRoundedIcon from '@mui/icons-material/SendRounded';
import SmartToyRoundedIcon from '@mui/icons-material/SmartToyRounded';
import PersonRoundedIcon from '@mui/icons-material/PersonRounded';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';

type Granularity = 'day' | 'week' | 'month';

type TrendPoint = {
  period: string;
  bucketStart: string;
  total: number;
};

type CategoryBreakdown = {
  category: string;
  total: number;
  count: number;
  percentage: number;
};

type AlertItem = {
  title: string;
  category: string;
  amount: number;
  average: number;
  deviation: number;
  severity: 'High' | 'Medium' | 'Low' | string;
  explanation: string;
  suggestion: string;
  createdAt: string;
  zScore: number;
  occurrences?: number;
};

type AiMessage = {
  role: 'user' | 'assistant';
  text: string;
};

type AiAskResponse = {
  answer: string;
  model?: string;
  fallbackReason?: string;
};

type MetricCardProps = {
  title: string;
  value: string;
  tone?: 'neutral' | 'positive' | 'warning';
};

const pieColors = ['#2f6fed', '#e95b2e', '#26a269', '#f29f05', '#7a57d1', '#00a3bf', '#8f9bb3'];

function formatCurrency(v: number): string {
  return 'USD ' + v.toFixed(2);
}

function MetricCard({ title, value, tone = 'neutral' }: MetricCardProps) {
  const color = tone === 'positive' ? '#1a9c5f' : tone === 'warning' ? '#d94841' : '#1f2a44';
  return (
    <Card sx={{ border: '1px solid #dbe2ef', borderRadius: 2 }}>
      <CardContent>
        <Typography variant="subtitle1" sx={{ color: '#354269', fontWeight: 600 }}>{title}</Typography>
        <Typography variant="h4" sx={{ mt: 1, color, fontWeight: 700 }}>{value}</Typography>
      </CardContent>
    </Card>
  );
}

function TrendLineChart({ points }: { points: TrendPoint[] }) {
  const width = 760;
  const height = 260;
  const left = 44;
  const right = 20;
  const top = 20;
  const bottom = 38;
  const chartW = width - left - right;
  const chartH = height - top - bottom;

  if (points.length === 0) {
    return <Typography color="text.secondary">No trend data.</Typography>;
  }

  const values = points.map((p) => p.total);
  const max = Math.max(...values, 1);
  const avg = values.reduce((a, b) => a + b, 0) / values.length;

  const coords = points.map((p, i) => {
    const x = left + (chartW * i) / Math.max(points.length - 1, 1);
    const y = top + chartH - (p.total / max) * chartH;
    return { x, y, label: p.period, hot: p.total > avg * 1.2 };
  });

  const line = coords.map((c) => c.x + ',' + c.y).join(' ');
  const area =
    'M ' + coords[0].x + ' ' + (top + chartH) +
    ' L ' + coords.map((c) => c.x + ' ' + c.y).join(' L ') +
    ' L ' + coords[coords.length - 1].x + ' ' + (top + chartH) +
    ' Z';

  return (
    <Box sx={{ width: '100%', overflowX: 'auto' }}>
      <svg viewBox={'0 0 ' + width + ' ' + height} width="100%" height="260" role="img" aria-label="expense trend line chart">
        <rect x="0" y="0" width={String(width)} height={String(height)} fill="#ffffff" />

        {[0, 0.25, 0.5, 0.75, 1].map((ratio) => {
          const y = top + chartH - ratio * chartH;
          return (
            <g key={String(ratio)}>
              <line x1={String(left)} y1={String(y)} x2={String(left + chartW)} y2={String(y)} stroke="#e6ebf5" />
              <text x="6" y={String(y + 4)} fontSize="10" fill="#76809a">{Math.round(max * ratio)}</text>
            </g>
          );
        })}

        <path d={area} fill="#2f6fed22" />
        <polyline points={line} fill="none" stroke="#2f6fed" strokeWidth="3" />

        {coords.map((c) => (
          <g key={c.label}>
            <circle cx={String(c.x)} cy={String(c.y)} r={c.hot ? '6' : '4'} fill={c.hot ? '#d94841' : '#2f6fed'} />
            <text x={String(c.x)} y={String(top + chartH + 20)} textAnchor="middle" fontSize="10" fill="#53607c">
              {c.label}
            </text>
          </g>
        ))}
      </svg>
    </Box>
  );
}

function CategoryPie({ items }: { items: CategoryBreakdown[] }) {
  const total = items.reduce((s, x) => s + x.percentage, 0);
  if (items.length === 0 || total <= 0) {
    return <Typography color="text.secondary">No category breakdown.</Typography>;
  }

  let current = 0;
  const segments = items
    .map((item, idx) => {
      const start = current;
      const end = current + item.percentage;
      current = end;
      return pieColors[idx % pieColors.length] + ' ' + start.toFixed(2) + '% ' + end.toFixed(2) + '%';
    })
    .join(', ');

  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '220px 1fr' }, gap: 2, alignItems: 'center' }}>
      <Box
        sx={{
          width: 220,
          height: 220,
          borderRadius: '50%',
          background: 'conic-gradient(' + segments + ')',
          border: '2px solid #e5ebf6',
          justifySelf: { xs: 'center', md: 'start' },
        }}
      />
      <Stack spacing={1}>
        {items.map((item, idx) => (
          <Stack key={item.category} direction="row" spacing={1} alignItems="center" justifyContent="space-between">
            <Stack direction="row" spacing={1} alignItems="center">
              <Box sx={{ width: 14, height: 14, borderRadius: 0.8, bgcolor: pieColors[idx % pieColors.length] }} />
              <Typography variant="body1">{item.category}</Typography>
            </Stack>
            <Typography variant="body1" fontWeight={700}>{item.percentage.toFixed(1)}%</Typography>
          </Stack>
        ))}
      </Stack>
    </Box>
  );
}

const severityChipColor: Record<string, 'error' | 'warning' | 'success' | 'default'> = {
  High: 'error',
  Medium: 'warning',
  Low: 'success',
};

const periodsByGranularity: Record<Granularity, number> = {
  day: 14,
  week: 12,
  month: 6,
};

const quickQuestions = [
  'Why did expenses increase?',
  'What is the biggest issue this month?',
  'How can I reduce costs?',
];

export default function InsightDashboardPage() {
  const [granularity, setGranularity] = useState<Granularity>('month');
  const [trends, setTrends] = useState<TrendPoint[]>([]);
  const [breakdown, setBreakdown] = useState<CategoryBreakdown[]>([]);
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [messages, setMessages] = useState<AiMessage[]>([
    {
      role: 'assistant',
      text: 'I can explain alerts and recommend actions based on your expense data. Select an alert or ask a question.',
    },
  ]);
  const [question, setQuestion] = useState('');
  const [asking, setAsking] = useState(false);
  const [activeAlert, setActiveAlert] = useState<AlertItem | null>(null);
  const [aiPanelOpen, setAiPanelOpen] = useState(false);
  const [aiModelLabel, setAiModelLabel] = useState<string | null>(null);
  const [aiFallbackReason, setAiFallbackReason] = useState<string | null>(null);
  const [hasAskedAi, setHasAskedAi] = useState(false);

  useEffect(() => {
    let disposed = false;

    async function load() {
      setLoading(true);
      setError(null);
      try {
        const periods = periodsByGranularity[granularity];
        const trendUrl = '/api/Insight/trends?granularity=' + granularity + '&periods=' + periods;

        const [trendResp, categoryResp, alertsResp] = await Promise.all([
          fetch(trendUrl),
          fetch('/api/Insight/category-breakdown?top=5'),
          fetch('/api/Alerts?includeLow=false&top=6'),
        ]);

        if (trendResp.ok === false || categoryResp.ok === false || alertsResp.ok === false) {
          throw new Error('Failed to load dashboard data.');
        }

        const trendData = (await trendResp.json()) as TrendPoint[];
        const categoryData = (await categoryResp.json()) as CategoryBreakdown[];
        const alertData = (await alertsResp.json()) as AlertItem[];

        if (disposed) return;

        setTrends(trendData);
        setBreakdown(categoryData);
        setAlerts(alertData);
      } catch {
        if (disposed === false) {
          setError('Unable to load insight dashboard data right now.');
        }
      } finally {
        if (disposed === false) {
          setLoading(false);
        }
      }
    }

    load();
    return () => {
      disposed = true;
    };
  }, [granularity]);

  async function askAi(rawQuestion: string, alertContext?: AlertItem | null) {
    const q = rawQuestion.trim();
    if (q.length === 0 || asking) return;

    if (alertContext) {
      setActiveAlert(alertContext);
    }

    setAiPanelOpen(true);
    setHasAskedAi(true);
    setMessages((prev) => [...prev, { role: 'user', text: q }]);
    setQuestion('');
    setAsking(true);

    try {
      const payload: any = { question: q };
      const context = alertContext ?? activeAlert;
      if (context) {
        payload.alertContext = {
          title: context.title,
          category: context.category,
          amount: context.amount,
          average: context.average,
          deviation: context.deviation,
          severity: context.severity,
          explanation: context.explanation,
          suggestion: context.suggestion,
        };
      }

      const resp = await fetch('/api/Ai/ask', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });

      if (resp.ok === false) {
        throw new Error('AI request failed');
      }

      const data = (await resp.json()) as AiAskResponse;
      setAiModelLabel(data.model ?? 'Unknown');
      setAiFallbackReason(data.fallbackReason ?? null);
      setMessages((prev) => [...prev, { role: 'assistant', text: data.answer }]);
    } catch {
      setAiModelLabel('Unavailable');
      setAiFallbackReason(null);
      setMessages((prev) => [
        ...prev,
        {
          role: 'assistant',
          text: 'I could not process that question right now. Please try again.',
        },
      ]);
    } finally {
      setAsking(false);
    }
  }

  const totalExpense = useMemo(() => trends.reduce((s, x) => s + x.total, 0), [trends]);

  const periodChange = useMemo(() => {
    if (trends.length < 2) return 0;
    const last = trends[trends.length - 1].total;
    const prev = trends[trends.length - 2].total;
    if (prev === 0) return 0;
    return ((last - prev) / prev) * 100;
  }, [trends]);

  const topCategory = useMemo(() => breakdown[0]?.category ?? 'N/A', [breakdown]);

  if (loading) {
    return (
      <Stack alignItems="center" sx={{ py: 8 }}>
        <CircularProgress />
        <Typography sx={{ mt: 2 }}>Loading insight dashboard...</Typography>
      </Stack>
    );
  }

  return (
    <Stack spacing={2.2} sx={{ position: 'relative' }}>
      <Typography variant="h4" fontWeight={800} sx={{ color: '#24364d' }}>Insight Dashboard</Typography>
      <Typography variant="body1" color="text.secondary">
        Alert-first decision support: detect, prioritize, explain, and guide action.
      </Typography>

      {error ? <Alert severity="error">{error}</Alert> : null}

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', lg: 'repeat(4,1fr)' }, gap: 2 }}>
        <MetricCard title="Total Expense" value={formatCurrency(totalExpense)} />
        <MetricCard
          title="Period Change"
          value={(periodChange >= 0 ? '+' : '') + periodChange.toFixed(1) + '%'}
          tone={periodChange >= 0 ? 'positive' : 'warning'}
        />
        <MetricCard title="Alerts" value={String(alerts.length) + ' active'} tone={alerts.length > 0 ? 'warning' : 'neutral'} />
        <MetricCard title="Top Category" value={topCategory} />
      </Box>

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: {
            xs: '1fr',
            xl: 'minmax(0, 2fr) 390px',
          },
          gap: 2,
          alignItems: 'start',
        }}
      >
        <Stack spacing={2}>
          <Card sx={{ border: '1px solid #dbe2ef', borderRadius: 2 }}>
            <CardContent>
              <Stack direction={{ xs: 'column', md: 'row' }} justifyContent="space-between" alignItems={{ xs: 'start', md: 'center' }} sx={{ mb: 1 }}>
                <Typography variant="h5" fontWeight={700} sx={{ color: '#24364d' }}>
                  Expense Trend
                </Typography>
                <ToggleButtonGroup
                  exclusive
                  size="small"
                  value={granularity}
                  onChange={(_, v: Granularity | null) => {
                    if (v) setGranularity(v);
                  }}
                >
                  <ToggleButton value="day">Day</ToggleButton>
                  <ToggleButton value="week">Week</ToggleButton>
                  <ToggleButton value="month">Month</ToggleButton>
                </ToggleButtonGroup>
              </Stack>
              <TrendLineChart points={trends} />
            </CardContent>
          </Card>

          <Card sx={{ border: '1px solid #dbe2ef', borderRadius: 2 }}>
            <CardContent>
              <Typography variant="h5" fontWeight={700} sx={{ color: '#24364d', mb: 2 }}>
                Category Breakdown
              </Typography>
              <CategoryPie items={breakdown} />
            </CardContent>
          </Card>
        </Stack>

        <Stack spacing={2} sx={{ position: { xl: 'sticky' }, top: { xl: 16 } }}>
          <Card sx={{ border: '1px solid #dbe2ef', borderRadius: 2 }}>
            <CardContent>
              <Typography variant="h5" fontWeight={700} sx={{ color: '#24364d', mb: 1.4 }}>
                Smart Alerts
              </Typography>

              <Stack spacing={1} sx={{ maxHeight: 560, overflowY: 'auto', pr: 0.5 }}>
                {alerts.map((a) => (
                  <Box
                    key={a.title + a.createdAt}
                    sx={{
                      border: '1px solid #e0e7f5',
                      borderRadius: 2,
                      p: 1.1,
                      bgcolor: '#fbfcff',
                    }}
                  >
                    <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.6, flexWrap: 'wrap' }}>
                      <Chip label={a.severity.toUpperCase()} size="small" color={severityChipColor[a.severity] ?? 'default'} />
                      {(a.occurrences ?? 1) > 1 ? <Chip label={String(a.occurrences) + 'x'} size="small" variant="outlined" /> : null}
                      <Typography variant="subtitle1" fontWeight={700}>{a.title}</Typography>
                    </Stack>

                    <Typography variant="body2" sx={{ mb: 0.45 }}>
                      <b>{formatCurrency(a.amount)}</b> (Avg: {formatCurrency(a.average)}) • {(a.deviation >= 0 ? '+' : '') + a.deviation.toFixed(1)}%
                    </Typography>
                    <Typography variant="body2" sx={{ mb: 0.35 }}>
                      <b>Why:</b> {a.explanation}
                    </Typography>
                    <Typography variant="body2" sx={{ mb: 0.7, color: '#2f6fed' }}>
                      <b>Action:</b> {a.suggestion}
                    </Typography>

                    <Button
                      size="small"
                      variant="contained"
                      onClick={() => askAi('Why is this expense unusually high?', a)}
                    >
                      Ask AI
                    </Button>
                  </Box>
                ))}
                {alerts.length === 0 ? <Typography color="text.secondary">No high/medium alerts.</Typography> : null}
              </Stack>
            </CardContent>
          </Card>

          {aiPanelOpen ? (
            <Slide direction="left" in={aiPanelOpen} mountOnEnter unmountOnExit>
              <Box sx={{ minWidth: 0 }}>
                <Card sx={{ border: '1px solid #dbe2ef', borderRadius: 3, overflow: 'hidden' }}>
                  <Box sx={{ px: 2, py: 1.5, bgcolor: '#f7f9ff', borderBottom: '1px solid #e2e8f6' }}>
                    <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={1}>
                      <Stack direction="row" alignItems="center" spacing={1}>
                        <Avatar sx={{ width: 32, height: 32, bgcolor: '#2f6fed' }}>
                          <SmartToyRoundedIcon fontSize="small" />
                        </Avatar>
                        <Box>
                          <Typography variant="h6" sx={{ fontWeight: 800, color: '#24364d' }}>AI Assistant</Typography>
                          {activeAlert ? (
                            <>
                              <Typography variant="caption" sx={{ color: '#4f5f80', display: 'block' }}>
                                Context: {activeAlert.title}
                              </Typography>
                              <Typography variant="caption" sx={{ color: '#4f5f80', display: 'block' }}>
                                Model: {hasAskedAi ? (aiModelLabel ?? '--') : '--'}
                              </Typography>
                              {aiModelLabel === 'Mock AI' && aiFallbackReason ? (
                                <Typography variant="caption" sx={{ color: '#b45309', display: 'block' }}>
                                  Fallback: {aiFallbackReason}
                                </Typography>
                              ) : null}
                            </>
                          ) : (
                            <>
                              <Typography variant="caption" sx={{ color: '#4f5f80', display: 'block' }}>
                                Context: Dashboard summary
                              </Typography>
                              <Typography variant="caption" sx={{ color: '#4f5f80', display: 'block' }}>
                                Model: {hasAskedAi ? (aiModelLabel ?? '--') : '--'}
                              </Typography>
                            </>
                          )}
                        </Box>
                      </Stack>

                      <IconButton size="small" onClick={() => setAiPanelOpen(false)}>
                        <CloseRoundedIcon />
                      </IconButton>
                    </Stack>
                  </Box>

                  <Box sx={{ p: 1.5, minHeight: 330, maxHeight: 420, overflowY: 'auto', bgcolor: '#ffffff' }}>
                    <Stack spacing={1.2}>
                      {messages.map((m, idx) => (
                        <Stack key={String(idx)} direction="row" spacing={1} alignItems="flex-start" justifyContent={m.role === 'user' ? 'flex-end' : 'flex-start'}>
                          {m.role === 'assistant' ? (
                            <Avatar sx={{ width: 28, height: 28, bgcolor: '#2f6fed' }}>
                              <SmartToyRoundedIcon sx={{ fontSize: 16 }} />
                            </Avatar>
                          ) : null}

                          <Box
                            sx={{
                              px: 1.6,
                              py: 1,
                              borderRadius: 2,
                              maxWidth: '85%',
                              bgcolor: m.role === 'user' ? '#e8efff' : '#f4f5f9',
                              border: m.role === 'user' ? '1px solid #d6e2ff' : '1px solid #e7e8ee',
                            }}
                          >
                            <Typography variant="body1">{m.text}</Typography>
                          </Box>

                          {m.role === 'user' ? (
                            <Avatar sx={{ width: 28, height: 28, bgcolor: '#8ab4f8' }}>
                              <PersonRoundedIcon sx={{ fontSize: 16 }} />
                            </Avatar>
                          ) : null}
                        </Stack>
                      ))}
                    </Stack>
                  </Box>

                  <Divider />
                  <Box sx={{ px: 1.5, pt: 1, pb: 1.2, bgcolor: '#ffffff' }}>
                    <Stack direction="row" spacing={1} sx={{ mb: 1, flexWrap: 'wrap' }}>
                      {quickQuestions.map((q) => (
                        <Button key={q} size="small" variant="outlined" onClick={() => askAi(q)} disabled={asking}>
                          {q}
                        </Button>
                      ))}
                    </Stack>

                    <Stack direction="row" spacing={1} alignItems="center">
                      <TextField
                        fullWidth
                        size="small"
                        placeholder="Type your question..."
                        value={question}
                        onChange={(e) => setQuestion(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter') {
                            askAi(question);
                          }
                        }}
                      />
                      <IconButton
                        color="primary"
                        onClick={() => askAi(question)}
                        disabled={asking}
                        sx={{
                          bgcolor: '#2f6fed',
                          color: '#fff',
                          '&:hover': { bgcolor: '#285fd0' },
                          '&.Mui-disabled': { bgcolor: '#b9c8f1', color: '#fff' },
                        }}
                      >
                        <SendRoundedIcon />
                      </IconButton>
                    </Stack>
                  </Box>
                </Card>
              </Box>
            </Slide>
          ) : (
            <Card sx={{ border: '1px solid #dbe2ef', borderRadius: 2 }}>
              <CardContent>
                <Stack direction="row" spacing={1.2} alignItems="center" justifyContent="space-between">
                  <Stack direction="row" spacing={1.2} alignItems="center">
                    <Avatar sx={{ width: 32, height: 32, bgcolor: '#2f6fed' }}>
                      <SmartToyRoundedIcon fontSize="small" />
                    </Avatar>
                    <Box>
                      <Typography variant="h6" sx={{ color: '#24364d', fontWeight: 800 }}>AI Assistant</Typography>
                      <Typography variant="body2" color="text.secondary">Ask questions and get action-focused insights.</Typography>
                      {hasAskedAi ? (
                        <Typography variant="caption" color="text.secondary">Model: {aiModelLabel ?? '--'}</Typography>
                      ) : null}
                      {aiModelLabel === 'Mock AI' && aiFallbackReason ? (
                        <Typography variant="caption" sx={{ color: '#b45309', display: 'block' }}>Fallback: {aiFallbackReason}</Typography>
                      ) : null}
                    </Box>
                  </Stack>
                  <Button variant="contained" size="small" onClick={() => setAiPanelOpen(true)}>
                    Open
                  </Button>
                </Stack>
              </CardContent>
            </Card>
          )}
        </Stack>
      </Box>

      {!aiPanelOpen ? (
        <Tooltip title="Ask AI">
          <IconButton
            onClick={() => setAiPanelOpen(true)}
            sx={{
              position: 'fixed',
              right: 22,
              bottom: 22,
              bgcolor: '#2f6fed',
              color: '#fff',
              width: 56,
              height: 56,
              boxShadow: '0 8px 20px rgba(47,111,237,0.35)',
              '&:hover': { bgcolor: '#285fd0' },
              zIndex: 1200,
            }}
          >
            <SmartToyRoundedIcon />
          </IconButton>
        </Tooltip>
      ) : null}
    </Stack>
  );
}
