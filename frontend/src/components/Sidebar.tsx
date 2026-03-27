// 左侧导航栏组件
import React from 'react';
import { Drawer, List, ListItem, ListItemIcon, ListItemText, Toolbar, Divider, ListItemButton } from '@mui/material';
import ReceiptIcon from '@mui/icons-material/Receipt';
import ListAltIcon from '@mui/icons-material/ListAlt';
import HistoryIcon from '@mui/icons-material/History';
import AddCircleOutlineIcon from '@mui/icons-material/AddCircleOutline';
import InsertChartIcon from '@mui/icons-material/InsertChart';
import { Link, useLocation } from 'react-router-dom';

const drawerWidth = 220;

const navItems = [
  { text: "账单列表", icon: <ReceiptIcon />, path: "/bills" },
  { text: "自动分类结果", icon: <ListAltIcon />, path: "/category" },
  { text: "异常检测历史", icon: <HistoryIcon />, path: "/anomaly" },
  { text: "新增账单录入", icon: <AddCircleOutlineIcon />, path: "/add" },
  { text: "统计分析", icon: <InsertChartIcon />, path: "/stats" },
];

const Sidebar: React.FC = () => {
  const location = useLocation();

  return (
    <Drawer
      variant="permanent"
      sx={{
        width: drawerWidth,
        flexShrink: 0,
        [`& .MuiDrawer-paper`]: { width: drawerWidth, boxSizing: 'border-box' },
      }}
    >
      <Toolbar />
      <Divider />
      <List>
        {navItems.map((item) => (
          <ListItem key={item.text} disablePadding>
            <ListItemButton
              component={Link}
              to={item.path}
              selected={location.pathname === item.path}
            >
              <ListItemIcon>{item.icon}</ListItemIcon>
              <ListItemText primary={item.text} />
            </ListItemButton>
          </ListItem>
        ))}
      </List>
    </Drawer>
  );
};

export default Sidebar;