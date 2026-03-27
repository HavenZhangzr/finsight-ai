import { styled } from '@mui/material/styles';
import MuiDrawer, { drawerClasses } from '@mui/material/Drawer';
import MenuContent from './MenuContent';

const drawerWidth = 240;
const paperSelector = '& .' + drawerClasses.paper;

const Drawer = styled(MuiDrawer)({
  width: drawerWidth,
  flexShrink: 0,
  boxSizing: 'border-box',
  [paperSelector]: {
    width: drawerWidth,
    boxSizing: 'border-box',
  },
});

export default function SideMenu() {
  return (
    <Drawer
      variant="permanent"
      sx={{
        display: { xs: 'none', md: 'block' },
        [paperSelector]: {
          backgroundColor: 'background.paper',
        },
      }}
    >
      <MenuContent />
    </Drawer>
  );
}
