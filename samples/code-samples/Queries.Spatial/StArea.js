// Find the area of a GeoJSON polygon using JavaScript.
function area(polygon) {
    var area = 0;
    var points = polygon.coordinates[0]; // Does not handle holes.
    var j = points.length - 1;
    var p1, p2;
    
    for (var i = 0; i < points.length; j = i++) {
        p1 = {
            x: points[i][1],
            y: points[i][0]
        };
        p2 = {
            x: points[j][1],
            y: points[j][0]
        };

        area += p1.x * p2.y;
        area -= p1.y * p2.x;
    }
    
    area /= 2;
    return area;
}
