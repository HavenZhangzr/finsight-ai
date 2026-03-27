import Grid from '@mui/material/Grid';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import CustomizedDataGrid from './CustomizedDataGrid'; // 只要它内容没写成 <Grid> item=... 就没问题！

const statCards = [
  { title: '今日账单', value: '128', interval: '今日', trend: 'up' },
  { title: '本月交易', value: '7,322', interval: '本月', trend: 'neutral' },
  { title: '异常数', value: '2', interval: '本月', trend: 'down' }
];

function StatCard({ title, value, interval }: { title: string, value: string, interval: string }) {
  return (
    <Box sx={{
      bgcolor: 'background.paper',
      borderRadius: 2,
      p: 2,
      boxShadow: 1,
      minHeight: 70,
      display: 'flex',
      flexDirection: 'column',
      justifyContent: 'center'
    }}>
      <Typography variant="subtitle2" color="text.secondary">{title}</Typography>
      <Typography variant="h6">{value}</Typography>
      <Typography variant="caption" color="text.secondary">{interval}</Typography>
    </Box>
  )
}

export default function MainGrid() {
  return (
    <Box sx={{ width: '100%', maxWidth: { sm: '100%', md: '1700px' } }}>
      <Typography component="h2" variant="h6" sx={{ mb: 2 }}>
        概览
      </Typography>
      <Grid container spacing={2} sx={{ mb: 2 }}>
        {statCards.map((card, i) => (
          <Grid item xs={12} sm={4} key={i}>
            <StatCard {...card} />
          </Grid>
        ))}
      </Grid>
      <Typography component="h2" variant="h6" sx={{ mb: 2 }}>
        账单列表
      </Typography>
      <Grid container spacing={2}>
        <Grid item xs={12}>
          <CustomizedDataGrid />
        </Grid>
      </Grid>
    </Box>
  );
}