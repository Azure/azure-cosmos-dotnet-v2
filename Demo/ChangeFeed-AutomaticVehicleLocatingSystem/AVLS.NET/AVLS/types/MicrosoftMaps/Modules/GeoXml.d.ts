/*
 * Copyright(c) 2017 Microsoft Corporation. All rights reserved. 
 * 
 * This code is licensed under the MIT License (MIT). 
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal 
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
 * of the Software, and to permit persons to whom the Software is furnished to do 
 * so, subject to the following conditions: 
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software. 
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE. 
*/

/// <reference path="../Microsoft.Maps.d.ts"/>

declare module Microsoft.Maps {
    
    /** 
    * An enumeration of different GeoXML file formats.
    * @requires The Microsoft.Maps.GeoXml module.
    */
    export enum GeoXmlFormat {
        /** A KML XML file format. */
        kml,

        /** A GPX XML file format. */
        gpx,

        /** A GeoRSS XML file using ATOM format. */
        geoRss
    }

    /**
     * An enumeration of the different compressed formats that XML can be outputted as.
    * @requires The Microsoft.Maps.GeoXml module.
     */
    export enum GeoXmlCompressedFormat {
        /** XML data compressed into a Base64 Data URI. */
        base64,

        /** XML data compressed into a Blob. */
        blob,

        /** XML data compressed into an ArrayBuffer. */
        arrayBuffer
    }

    /**
     * A GeoXML result data set that is returned when reading a spatial XML file.
     */
    export interface IGeoXmlDataSet {
        /** An array of shapes that are in the XML document. */
        shapes?: IPrimitive[];

        /**
        * An array of layers that are in the XML document.
        * In shapes in KML that are in a child documents and folders of the main document or folder are grouped together in a data Layer.
        * KML also supports GroundOverlay.
        */
        layers?: (Layer | GroundOverlay)[];

        /**
        * An array of screen overlays that have been parsed from a KML file.
        */
        screenOverlays?: KmlScreenOverlay[];

        /**
        * Summary metadata provided at the document level of the XML feed data set.
        */
        summary?: IGeoXmlSummaryMetadata;

        /**
         * Statistics about the content and processing time of a XML feed.
         */
        stats?: IGeoXmlStats;
    }

    /**
     * Options used to customize how a GeoXmlLayer renders.
     */
    export interface IGeoXmlLayerOptions extends IGeoXmlReadOptions {
        /** A boolean indicating if the map should automatically upate the view when a data set is loaded. Default: true */
        autoUpdateMapView?: boolean;

        /** Options used to customize how the default infobox renders. */
        infoboxOptions?: Microsoft.Maps.IInfoboxOptions;

        /** A boolean indicating if infoboxes should automatically appear when shapes clicked. Default: false */
        suppressInfoboxes?: boolean;

        /** A boolean indicating if the layer is visible or not. Default: true */
        visible?: boolean;
    }

    /**
     * Options that customize how XML files are read and parsed.
     */
    export interface IGeoXmlReadOptions {
        /** Specifies if KML ScreenOverlays should be read or ignored. Default: true */
        allowKmlScreenOverlays?: boolean;

        /**
        * A callback function that is triggered when an error occurs when reading an XML document.
        */
        error?: (msg: string) => void;

        /**
        * Specifies wether the individual waypoint data of a GPX Route or Track should be captured.
        * If set to true, the shape will have a metadata.waypoints property that is an array of
        * pushpins that contains the details of each waypoint along the track. Default: false
        */
        captureGpxPathWaypoints?: boolean;

        /** The default styles to apply to shapes that don't have a defined style in the XML. */
        defaultStyles?: IStylesOptions;

        /** Specifies if shapes visible tags should be used to set the visible property of it's equivalent Bing Maps shape. Default: true */
        ignoreVisibility?: boolean;

        /**
        * The maximium number of network links that a single KML file can have. Default: 10.
        */
        maxNetworkLinks?: number;

        /**
         * The maximium depth of network links in a KML file. Default: 3
         * Example: when set to 3; file1 links to file2 which links to file3 but won't open links in file3.
         */
        maxNetworkLinkDepth?: number;

        /** Indicates if the pushpin title should be displayed on the map if a valid title or name value exits in the shapes metadata. Default: true */
        setPushpinTitles?: boolean;
    }

    /**
     * Statistics about the content and processing time of a XML feed.
     */
    export interface IGeoXmlStats {
        /** Number of Microsoft.Maps.Pushpins objects in the XML feed. */
        numPushpins: number;

        /** Number of Microsoft.Maps.Polylines objects in the XML feed. */
        numPolylines: number;

        /** Number of Microsoft.Maps.Polygons objects in the XML feed. */
        numPolygons: number;

        /** Number of Microsoft.Maps.Location objects in the XML feed. */
        numLocations: number;

        /** Number of Ground Overlays in the XML feed. */
        numGroundOverlays: number;

        /** Number of Screen Overlays in the XML feed. */
        numScreenOverlays: number;

        /** The number of network links in the XML feed. */
        numNetworkLinks: number;

        /** The number of characters in the XML feed. */
        fileSize: number;

        /** The amount of time in ms it took to process the XML feed. */
        processingTime: number;
    }

    /**
     * Summary metadata provided at the document level of the XML feed data set.
     */
    export interface IGeoXmlSummaryMetadata {
        /** The title or name of the content of the XML document. */
        title?: string;

        /** The description of the content of the XML document. */
        description?: string;

        /** The bounds of all the shapes and layers in the XML document. */
        bounds?: LocationRect;

        /** Any additional metadata that the XML document may have. i.e. atom:author */
        metadata?: IDictionary<any>;
    }

    /**
     * Options that are used to customize how the GeoXml writes XML.
     */
    export interface IGeoXmlWriteOptions {

        /** The characters to use to create an indent in the XML data. Default: \t */
        indentChars?: string;

        /** The characters to use to create a new line in the XML data. Default: \r\n */
        newLineChars?: string;

        /** A boolean indicating if the generated XML should be use new lines and indents to make the generated nicely formatted. Default: true */
        prettyPrint?: boolean;

        /** A boolean indicating if Location and LocationRect values should be rounded off to 6 decimals. Default: false */
        roundLocations?: boolean;

        /**
         * A boolean indicating if the shapes should be made valid before writing. If set to true, will use the
         * Geometry.makeValid function of the SpatialMath module. Default: false
         */
        validate?: boolean;

        /** The XML format to write the shapes to. Default: Kml */
        xmlFormat?: GeoXmlFormat;
    }
    
    /**
    * The options for customizing screen overlays.
    */
    export interface IKmlScreenOverlayOptions {
        /** A boolean indicating if the screen overlay can be displayed above or beow the navigaiton bar. Default: false */
        belowNavigationBar?: boolean;

        /** The visibility of the screen overlay. Default: true */
        visible?: boolean;
    }

    /**
     * Overlays HTML elements winthin the map container that are above the map.
     * This is useful when adding logos, attributions or legends to the map.
     * This class is only used by the KML reader and not meant to be used on its own.
     * @requires The Microsoft.Maps.GeoXml module.
     */
    export class KmlScreenOverlay {

        /** Optional property to store any additional metadata for this overlay. */
        metadata: any;

        /**
         * @constructor
         * @param htmlElement The new htmlElement to set for the overlay.
         * @param options The options to customize the screen overlay.
         */
        constructor(htmlElement?: string | HTMLElement, options?: IKmlScreenOverlayOptions);
        
        /**
        * Clears the screen overlay.
        */
        public clear(): void;

        /**
         * Gets a boolean indicating if the screen overlay is displayed above or below the navigation bar.
         * @returns A boolean indicating if the screen overlay is displayed above or below the navigation bar.
         */
        public getBelowNavigationBar(): boolean;

        /**
        * Gets the html element of this screen overlay.
        * @returns the htmlElement of this overlay.
        */
        public getHtmlElement(): HTMLElement;

        /**
         * Returns the map that this overlay is attached to.
         * @returns The map that this overlay is attached to.
         */
        public getMap(): Map;

        /**
         * Gets a boolean indicating if the screen overlay is visible or not.
         * @returns A boolean indicating if the screen overlay is visible or not.
         */
        public getVisible(): boolean;

        /**
         * Updates the html element of this screen overlay.
         * @param htmlElement The new htmlElement to set for the overlay.
         */
        public setHtmlElement(htmlElement: string | HTMLElement): void;

        /**
         * Sets the options to customize the screen overlay.
         * @param options The options to customize the screen overlay.
         */
        public setOptions(options: IKmlScreenOverlayOptions): void;

        /**
         * Sets whether the overlay is visible or not.
         * @param value A value indicating if the overlay should be displayed or not.
         */
        public setVisible(visible: boolean): void;
    }
    
    /**
     * A static class that contains functions for reading and writing geospatial XML data.
     * @requires The Microsoft.Maps.GeoXml module.
     */
    export module GeoXml {
        
        /**
         * Takes a geospatial XML string or a ArrayBuffer and parses the XML data into Bing Maps shapes.
         * @param xml The XML as a string or ArrayBuffer to read.
         * @param options The read options.
         */
        export function read(xml: string | ArrayBuffer, options: IGeoXmlReadOptions): IGeoXmlDataSet;
        
        /**
         * Takes an URL to an XML or zipped XML file and parses the XML data into Bing Maps shapes.
         * @param xml The URL to XML data to read.
         * @param options The read options.
         * @param callback The callback function that consumes the parsed out GeoXml data.
         */
        export function readFromUrl(urlString: string, options: IGeoXmlReadOptions, callback: (data: IGeoXmlDataSet) => void): void;

        /**
         * Writes Bing Maps shape data as a geospatial XML string in the specified format.
         * @param shapes The Bing Maps shapes, or map to retrieve shapes from, to write.
         * @param options A set of options that customize how the XML is writen.
         */
        export function write(shapes: Map | IPrimitive | IPrimitive[] | Layer | GroundOverlay[] | IGeoXmlDataSet, options?: IGeoXmlWriteOptions): string;
        
        /**
         * Writes Bing Maps shape data to a geospatial XML file embedded in a compressed file.
         * @param shapes The Bing Maps shapes, or map to retrieve shapes from, to write.
         * @param compressFormat The compressed file format to use.
         * @param options A set of options that customize how the XML is writen.
         */
        export function writeCompressed(shapes: Map | IPrimitive | IPrimitive[] | Layer | GroundOverlay[] | IGeoXmlDataSet, compressFormat?: GeoXmlCompressedFormat, options?: IGeoXmlWriteOptions): string | ArrayBuffer | Blob;
    }

    /**
     * A layer that loads and renders geospatial XML data on the map.
     * @requires The Microsoft.Maps.GeoXml module.
     */
    export class GeoXmlLayer extends Microsoft.Maps.CustomOverlay {

        /** Optional property to store any additional metadata for this layer. */
        metadata: any;

        /**
         * @constructor
         * @param dataSource The XML as a string, URL or ArrayBuffer to read.
         * @param isUrl Whether the dataSource provided is an URL. Default = true
         * @param options The options used to render the layer.
         */
        constructor(dataSource?: string | ArrayBuffer, isUrl?: boolean, options?: IGeoXmlLayerOptions);
        
        /**
         * Removes all the data in the layer.
         */
        public clear(): void;

        /**
         * Cleans up any resources this object is consuming.
         */
        public dispose(): void;

        /**
         * Returns the data source used by the layer.
         * @returns The data source used by the layer.
         */
        public getDataSource(): string | ArrayBuffer;

        /**
         * Returns the data set that ws extracted from the data source.
         * @returns The data set that ws extracted from the data source.
         */
        public getDataSet(): IGeoXmlDataSet;

        /**
         * Returns the options used by the GeoXmlLayer.
         * @returns The options used by the GeoXmlLayer.
         */
        public getOptions(): IGeoXmlLayerOptions;

        /**
         * Gets a value indicating whether the layer is visible or not.
         * @returns A boolean indicating if the layer is visible or not.
         */
        public getVisible(): boolean;

        /**
         * Sets the data source to render in the GeoXmlLayer.
         * @param dataSource The data source to render in the GeoXmlLayer.
         * @param isUrl Whether the dataSource provided is an URL. Default = true
         */
        public setDataSource(dataSource: string | ArrayBuffer, isUrl: boolean): void;

        /**
         * Sets the options used for loading and rendering data into the GeoXmlLayer.
         * @param options The options to use when loading and rendering data into the GeoXmlLayer.
         */
        public setOptions(options: IGeoXmlLayerOptions): void;

        /**
         * Sets whether the layer is visible or not.
         * @param value A value indicating if the layer should be displayed or not.
         */
        public setVisible(visible: boolean): void;
    }


    export module Events {
        /////////////////////////////////////
        /// addHandler Definitions
        ////////////////////////////////////

        /**
        * Attaches the handler for the event that is thrown by the target. Use the return object to remove the handler using the removeHandler method.
        * @param target The object to attach the event to; Map, IPrimitive, Infobox, Layer, DrawingTools, DrawingManager, DirectionsManager, etc.
        * @param eventName The type of event to attach. Supported Events:
        * click, dblclick, mousedown, mouseout, mouseover, mouseup, rightclick
        * @param handler The callback function to handle the event when triggered. 
        * @returns The handler id.
        */
        export function addHandler(target: GeoXmlLayer, eventName: string, handler: (eventArg?: IMouseEventArgs) => void): IHandlerId;
        
        /////////////////////////////////////
        /// addOne Definitions
        ////////////////////////////////////
        
        /**
         * Attaches the handler for the event that is thrown by the target, but only triggers the handler the first once after being attached.
         * @param target The object to attach the event to; Map, IPrimitive, Infobox, Layer, DrawingTools, DrawingManager, DirectionsManager, etc.
         * @param eventName The type of event to attach. Supported Events:
         * click, dblclick, mousedown, mouseout, mouseover, mouseup, rightclick
         * @param handler The callback function to handle the event when triggered.
         */
        export function addOne(target: GeoXmlLayer, eventName: string, handler: (eventArg?: IMouseEventArgs) => void): void;

        /////////////////////////////////////
        /// addThrottledHandler Definitions
        ////////////////////////////////////

        /**
        * Attaches the handler for the event that is thrown by the target, where the minimum interval between events (in milliseconds) is specified as a parameter.
        * @param target The object to attach the event to; Map, IPrimitive, Infobox, Layer, DrawingTools, DrawingManager, DirectionsManager, etc.
        * @param eventName The type of event to attach. Supported Events:
        * click, dblclick, mousedown, mouseout, mouseover, mouseup, rightclick
        * @param handler The callback function to handle the event when triggered. 
        * @returns The handler id.
        */
        export function addThrottledHandler(target: GeoXmlLayer, eventName: string, handler: (eventArg?: IMouseEventArgs) => void): IHandlerId;
    }
}
