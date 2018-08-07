/// <reference path="../types/microsoftmaps/microsoft.maps.all.d.ts" />
//https://github.com/Microsoft/Bing-Maps-V8-TypeScript-Definitions

class BingMap {

    map;
    error: string;

    /*
    Get Type definitions for Microsoft.Maps 8.0 (Change set e6d7cc4)
    Project: https://github.com/Microsoft/Bing-Maps-V8-TypeScript-Definitions
    Definitions by: Ricky Brundritt <https://github.com/rbrundritt>
    Definitions: https://github.com/DefinitelyTyped/DefinitelyTyped

    Install-Package Microsoft.BingMaps.V8.TypeScript
    */


    GetMap() {
        if (this.map == null) {
            this.map = new Microsoft.Maps.Map(document.getElementById('myMap'), {
                credentials: 'AlPML0DUy5EymkKvwKL7WozXhT-1ikp0FUPyDMNWY7VPxtgZgMHP3gS9KAjEOKsx',
                center: new Microsoft.Maps.Location(46.603787, -122.185023),
                zoom: 10
            });

            var loc = new Microsoft.Maps.Location( //Default location
                47.639002, -122.128196);
            this.map.entities.clear();
            var center = this.map.getCenter();
            this.map.setView({ center: loc });

        }
    }

    lastResponse: string = "";
    // response  { "lat" : "40.7145500183105", "long": "-74.0071411132813", "carId": "ABC 432" }
    DrawMap(resp: any) {
        if (this.map === undefined) {
            this.GetMap();
        }

        if (resp != this.lastResponse) {
            this.lastResponse = resp;
        } else {
            return; //No need to refresh
        }
       
        if (resp) {
            var loc = new Microsoft.Maps.Location(
                parseFloat(resp.lat),
                parseFloat(resp.long));

            var center = this.map.getCenter();
            var pin = new Microsoft.Maps.Pushpin(center, {
                title: 'Accident',
                subTitle: resp.carid,
                color: 'red',
            });

            pin.setLocation(loc);
            pin.setOptions({ visible: true });
            this.map.setView({ center: loc , zoom: 12 });
            this.map.entities.clear();
            this.map.entities.push(pin);
        } 
    }
}