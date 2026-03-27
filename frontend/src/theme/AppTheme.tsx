import * as React from 'react';
import { ThemeProvider, createTheme } from '@mui/material/styles';

interface AppThemeProps {
  children: React.ReactNode;
}

export default function AppTheme(props: AppThemeProps) {
  const { children } = props;
  // 只用最简基础主题，你想自定义主色/圆角/字体可直接在这里写
  const theme = React.useMemo(() =>
    createTheme({
      palette: {
        primary: { main: '#1976d2' },   // 自定义主色
        secondary: { main: '#19857b' }, // 可选次要色
        background: { default: '#f4f6fa', paper: '#fff' }
      },
      shape: { borderRadius: 12 },
      typography: {
        fontFamily: [
          '"Segoe UI"', '"Roboto"', '"PingFang SC"', 'sans-serif'
        ].join(','),
      },
    })
  , []);
  
  return (
    <ThemeProvider theme={theme}>
      {children}
    </ThemeProvider>
  );
}