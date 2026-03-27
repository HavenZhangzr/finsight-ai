import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';

export default function Header() {
  return (
    <Stack
      direction="row"
      sx={{
        width: '100%',
        alignItems: 'center',
        justifyContent: 'flex-start',
        pt: 2,
        pl: 2,
      }}
    >
      <Typography variant="h5" sx={{ fontWeight: 700 }}>
        欢迎使用账单智能检测系统
      </Typography>
    </Stack>
  );
}