SET IDENTITY_INSERT line ON; 
GO

INSERT INTO line (id, line, line_production, process, name, remark) VALUES
(1,  'L1', 'Line A', 'Winding', 'T9 - 2273', 'Mesin Utama'),
(2,  'L1', 'Line A', 'Pack',    'T9 - 1792', 'Packaging Unit'),
(3,  'L1', 'Line A', 'Cutting', 'T9 - 3001', 'Auto Cutter'),
(4,  'L1', 'Line A', 'Pressing','T9 - 3002', 'High Pressure'),
(5,  'L1', 'Line A', 'Testing', 'T9 - 3003', 'Quality Control'),

(6,  'L2', 'Line B', 'Winding', 'T9 - 4001', 'Core Winder'),
(7,  'L2', 'Line B', 'Soldering','T9 - 4002', 'Robot Solder'),
(8,  'L2', 'Line B', 'Assembly','T9 - 4003', 'Main Assembly'),
(9,  'L2', 'Line B', 'Testing', 'T9 - 4004', 'Leak Test'),
(10, 'L2', 'Line B', 'Pack',    'T9 - 4005', 'Final Pack'),

(11, 'L3', 'Line C', 'Molding', 'T9 - 5001', 'Injection Mold'),
(12, 'L3', 'Line C', 'Molding', 'T9 - 5002', 'Injection Mold'),
(13, 'L3', 'Line C', 'Cooling', 'T9 - 5003', 'Cooling Tunnel'),
(14, 'L3', 'Line C', 'Trimming','T9 - 5004', 'Auto Trim'),
(15, 'L3', 'Line C', 'Inspection','T9 - 5005', 'Vision System'),

(16, 'L4', 'Line D', 'Stamping','T9 - 6001', 'Metal Stamp'),
(17, 'L4', 'Line D', 'Stamping','T9 - 6002', 'Metal Stamp'),
(18, 'L4', 'Line D', 'Welding', 'T9 - 6003', 'Spot Welder'),
(19, 'L4', 'Line D', 'Painting','T9 - 6004', 'Spray Paint'),
(20, 'L4', 'Line D', 'Drying',  'T9 - 6005', 'Oven Dryer'),

(21, 'L5', 'Line E', 'Mixing',  'T9 - 7001', 'Mixer A'),
(22, 'L5', 'Line E', 'Filling', 'T9 - 7002', 'Auto Filler'),
(23, 'L5', 'Line E', 'Capping', 'T9 - 7003', 'Capper'),
(24, 'L5', 'Line E', 'Labeling','T9 - 7004', 'Labeler'),
(25, 'L5', 'Line E', 'Packing', 'T9 - 7005', 'Cartoner'),

-- Sisa data random untuk memenuhi kuota 50 data
(26, 'L6', 'Line F', 'CNC',     'T9 - 8001', '-'),
(27, 'L6', 'Line F', 'CNC',     'T9 - 8002', '-'),
(28, 'L6', 'Line F', 'Lathe',   'T9 - 8003', '-'),
(29, 'L6', 'Line F', 'Milling', 'T9 - 8004', '-'),
(30, 'L6', 'Line F', 'Grinding','T9 - 8005', '-'),

(31, 'L7', 'Line G', 'SMT',     'T9 - 9001', 'Pick & Place'),
(32, 'L7', 'Line G', 'SMT',     'T9 - 9002', 'Reflow Oven'),
(33, 'L7', 'Line G', 'AOI',     'T9 - 9003', 'Optical Inspect'),
(34, 'L7', 'Line G', 'ICT',     'T9 - 9004', 'In-Circuit Test'),
(35, 'L7', 'Line G', 'FCT',     'T9 - 9005', 'Func Test'),

(36, 'L8', 'Line H', 'Prep',    'T9 - 1001', '-'),
(37, 'L8', 'Line H', 'Sewing',  'T9 - 1002', '-'),
(38, 'L8', 'Line H', 'Sewing',  'T9 - 1003', '-'),
(39, 'L8', 'Line H', 'Ironing', 'T9 - 1004', '-'),
(40, 'L8', 'Line H', 'Folding', 'T9 - 1005', '-'),

(41, 'L9', 'Line I', 'Extrusion','T9 - 1101', '-'),
(42, 'L9', 'Line I', 'Cutting', 'T9 - 1102', '-'),
(43, 'L9', 'Line I', 'Printing','T9 - 1103', '-'),
(44, 'L9', 'Line I', 'Laminating','T9 - 1104', '-'),
(45, 'L9', 'Line I', 'Slitting','T9 - 1105', '-'),

(46, 'L10', 'Line J', 'Receiving','T9 - 1201', 'Warehouse'),
(47, 'L10', 'Line J', 'Sorting',  'T9 - 1202', 'Sortation'),
(48, 'L10', 'Line J', 'Storage',  'T9 - 1203', 'Rack A'),
(49, 'L10', 'Line J', 'Picking',  'T9 - 1204', 'AGV Robot'),
(50, 'L10', 'Line J', 'Shipping', 'T9 - 1205', 'Dock 1');

SET IDENTITY_INSERT line OFF;
GO
