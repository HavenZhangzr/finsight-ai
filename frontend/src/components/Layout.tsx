// 通用布局（包含侧栏、头部和内容区）
import React from 'react';
import Box from '@mui/material/Box';
import Toolbar from '@mui/material/Toolbar';
import Sidebar from './Sidebar';
import Header from './Header';

interface LayoutProps {
  children: React.ReactNode;
}

const drawerWidth = 220;

const Layout: React.FC<LayoutProps> = ({ children }) => {
  return (
    <Box sx={{ display: 'flex' }}>
      <Header />
      <Sidebar />
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          bgcolor: 'background.default',
          p: 3,
          ml: `${drawerWidth}px`,
          minHeight: '100vh'
        }}
      >
        {/* Fixed AppBar 占位 */}
        <Toolbar />
        {children}
      </Box>
    </Box>
  );
};

export default Layout;