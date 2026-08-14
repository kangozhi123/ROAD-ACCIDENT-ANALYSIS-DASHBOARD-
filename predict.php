<?php
require_once __DIR__ . '/db.php';
session_start();
header('Content-Type: application/json');
if (!isset($_SESSION['user'])) {
    http_response_code(401);
    echo json_encode(['error' => 'Unauthorized. Please log in first.']);
    exit;
}

$data = json_decode(file_get_contents('php://input'), true);

if (!$data) {
    http_response_code(400);
    echo json_encode(['error' => 'Invalid request payload']);
    exit;
}

$area = $data['area'] ?? '';
$month = $data['month'] ?? '';
$weather = $data['weather'] ?? '';

if ($area === '' || $month === '' || $weather === '') {
    http_response_code(400);
    echo json_encode(['error' => 'Missing prediction input fields']);
    exit;
}

// Simple risk scoring logic for demo purposes.
$baseRisk = 3;
$areaFactor = in_array($area, ['Parklands', 'Wusakile', 'Nkana East']) ? 2 : 1;
$monthFactor = in_array($month, ['July', 'August', 'September']) ? 2 : 1;
$weatherFactor = $weather === 'rainy' ? 2 : 1;

$riskScore = $baseRisk * $areaFactor * $monthFactor * $weatherFactor;
$predictionText = "Predicted risk level for $area in $month with " . ($weather === 'rainy' ? 'wet' : 'dry') . " conditions is ";

if ($riskScore >= 20) {
    $predictionText .= 'High. Increased patrols and caution advised.';
} elseif ($riskScore >= 12) {
    $predictionText .= 'Medium. Monitor and enforce speed control.';
} else {
    $predictionText .= 'Low. Continue standard monitoring.';
}

echo json_encode(['prediction' => $predictionText]);
