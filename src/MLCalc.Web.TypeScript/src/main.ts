// The TypeScript client of MLCalc: the same page as the Blazor client, with the same stylesheet.
import '../../MLCalc.Web.Client/wwwroot/css/app.css';
import iconUrl from '../../MLCalc.Web.Client/wwwroot/icon.svg';
import { App } from './ui/app';

const icon = document.createElement('link');
icon.rel = 'icon';
icon.type = 'image/svg+xml';
icon.href = iconUrl;
document.head.append(icon);

const app = new App(document.getElementById('app')!);
void app.start();
