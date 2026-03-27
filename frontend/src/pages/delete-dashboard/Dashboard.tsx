import CssBaseline from '@mui/material/CssBaseline';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import AppNavbar from '../../delete-layout/AppNavbar';
import Header from '../../delete-layout/Header';
import SideMenu from '../../delete-layout/SideMenu';
import AppTheme from '../../theme/AppTheme';
// import MainGrid from './layout/MainGrid'; // 无内容时可暂不引入

export default function Dashboard() {
  return (
    <AppTheme>
      <CssBaseline enableColorScheme />
      <Box sx={{ display: 'flex' }}>
        <SideMenu />
        <Box sx={{ flexGrow: 1 }}>
          <AppNavbar />
          <Box
            component="main"
            sx={{
              flexGrow: 1,
              bgcolor: 'background.default',
              minHeight: '100vh',
              overflow: 'auto',
              p: 3,
            }}
          >
            <Stack spacing={2} alignItems="center">
              <Header />
              {/* <MainGrid /> */}
              {/* 你的主页面内容可以替换这里 */}
            </Stack>
          </Box>
        </Box>
      </Box>
    </AppTheme>
  );
}